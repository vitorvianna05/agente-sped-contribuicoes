using System.Text;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed partial class KnowledgeRepository(RagDataSource source, TimeProvider clock) : IKnowledgeStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task AuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string actor, string? organization,
        string action, Guid id, string result, DateTimeOffset at, string correlation, CancellationToken cancellationToken) =>
        await new KnowledgeAuditWriter(connection, transaction).WriteAsync(new AuditEntry(Guid.NewGuid(), actor, organization, action, id, result, at, correlation), cancellationToken);

    private static GeracaoProcessamento Generation(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3),
        new ContentHash(r.GetString(4)), r.GetString(5), r.GetFieldValue<DateTimeOffset>(6),
        r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8),
        r.IsDBNull(9) ? null : new ContentHash(r.GetString(9)), r.IsDBNull(10) ? null : r.GetString(10));

    private const string GenerationColumns = "g.generation_id,g.document_version_id,g.operation_id,g.profile_id,g.source_hash,g.state,g.created_at,g.completed_at,g.superseded_at,g.content_manifest_hash,g.failure_code";

    public async Task<EmbeddingJob?> ClaimEmbeddingAsync(DateTimeOffset now, TimeSpan lease, int maximumAttempts, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var select = new NpgsqlCommand($"""
            SELECT {GenerationColumns},p.definition,g.embedding_state,g.embedding_attempts,g.embedding_owner,g.embedding_until,o.correlation_id
            FROM rag.generations g JOIN rag.processing_profiles p USING(profile_id) JOIN rag.operations o ON o.operation_id=g.operation_id
            WHERE g.preparation_state='ready' AND g.state='processing' AND
              (g.embedding_state='queued' OR (g.embedding_state='processing' AND g.embedding_until<=@now))
            ORDER BY g.created_at FOR UPDATE OF g SKIP LOCKED LIMIT 1
            """, connection, transaction);
        select.Parameters.AddWithValue("now", now);
        GeracaoProcessamento generation;
        PerfilProcessamento profile;
        VectorProcessing state;
        string correlation;
        await using (var r = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (!await r.ReadAsync(cancellationToken))
            {
                return null;
            }

            generation = Generation(r);
            profile = JsonSerializer.Deserialize<PerfilProcessamento>(r.GetString(11), Json)!;
            state = new VectorProcessing(r.GetString(12), r.GetInt32(13), r.IsDBNull(14) ? null : r.GetGuid(14), r.IsDBNull(15) ? null : r.GetFieldValue<DateTimeOffset>(15));
            correlation = r.GetString(16);
        }
        var owner = Guid.NewGuid();
        try { state = state.Claim(owner, now, lease, maximumAttempts); }
        catch (KnowledgeException exception) when (exception.Code == "PROCESSING_ATTEMPTS_EXHAUSTED")
        {
            await FinishVectorStateAsync(connection, transaction, generation, exception.Code, now, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var operation = await new OperationRepository(connection, transaction).GetForUpdateAsync(generation.OperationId, cancellationToken);
        operation = operation.StartPreparation(owner, now, lease) with { ResultCode = "EMBEDDINGS_PROCESSING" };
        await using var update = new NpgsqlCommand("""
            UPDATE rag.generations SET embedding_state=@state,embedding_attempts=@attempt,embedding_owner=@owner,embedding_until=@until WHERE generation_id=@id;
            UPDATE rag.operations SET state=@state,started_at=@started,lease_owner=@ownerText,lease_until=@until,attempt_number=@operationAttempt,result_code=@result WHERE operation_id=@operation
            """, connection, transaction);
        update.Parameters.AddWithValue("state", state.State);
        update.Parameters.AddWithValue("attempt", state.Attempts);
        update.Parameters.AddWithValue("owner", owner);
        update.Parameters.AddWithValue("ownerText", owner.ToString());
        update.Parameters.AddWithValue("until", state.Until!.Value);
        update.Parameters.AddWithValue("id", generation.GenerationId);
        update.Parameters.AddWithValue("operation", operation.OperationId);
        update.Parameters.AddWithValue("started", operation.StartedAt!.Value);
        update.Parameters.AddWithValue("operationAttempt", operation.AttemptNumber);
        update.Parameters.AddWithValue("result", operation.ResultCode!);
        await update.ExecuteNonQueryAsync(cancellationToken);
        var chunks = new List<VectorChunk>();
        await using var load = new NpgsqlCommand("SELECT chunk_id,text,embedding FROM rag.chunks WHERE generation_id=@id ORDER BY ordinal", connection, transaction);
        load.Parameters.AddWithValue("id", generation.GenerationId);
        await using (var r = await load.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                chunks.Add(new(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetFieldValue<Vector>(2).ToArray()));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new EmbeddingJob(generation.GenerationId, generation.OperationId, owner, profile, chunks.ToArray(), correlation);
    }

    private static async Task<(GeracaoProcessamento Generation, VectorProcessing State)> LockEmbeddingAsync(NpgsqlConnection connection,
        NpgsqlTransaction transaction, EmbeddingJob job, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand($"SELECT {GenerationColumns},embedding_state,embedding_attempts,embedding_owner,embedding_until FROM rag.generations g WHERE generation_id=@id FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue("id", job.GenerationId);
        await using var r = await command.ExecuteReaderAsync(cancellationToken);
        if (!await r.ReadAsync(cancellationToken))
        {
            throw new KnowledgeException("RESOURCE_NOT_FOUND");
        }

        var generation = Generation(r);
        var state = new VectorProcessing(r.GetString(11), r.GetInt32(12), r.IsDBNull(13) ? null : r.GetGuid(13), r.IsDBNull(14) ? null : r.GetFieldValue<DateTimeOffset>(14));
        state.RequireOwner(job.Owner, at);
        if (generation.OperationId != job.OperationId || generation.ProfileId != job.Profile.ProfileId)
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }

        return (generation, state);
    }

    public async Task SaveVectorAsync(EmbeddingJob job, Guid chunkId, float[] vector, DateTimeOffset at, CancellationToken cancellationToken)
    {
        VectorProcessing.ValidateVector(vector, job.Profile.EmbeddingDimensions);
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockEmbeddingAsync(connection, transaction, job, at, cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE rag.chunks SET embedding=@vector WHERE chunk_id=@chunk AND generation_id=@generation AND embedding IS NULL", connection, transaction);
        command.Parameters.AddWithValue("vector", new Vector(vector));
        command.Parameters.AddWithValue("chunk", chunkId);
        command.Parameters.AddWithValue("generation", job.GenerationId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteEmbeddingAsync(EmbeddingJob job, string? failure, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var (generation, _) = await LockEmbeddingAsync(connection, transaction, job, at, cancellationToken);
        ContentHash? manifest = null;
        if (failure is null)
        {
            await using var complete = new NpgsqlCommand("SELECT text_hash,embedding FROM rag.chunks WHERE generation_id=@id ORDER BY ordinal", connection, transaction);
            complete.Parameters.AddWithValue("id", job.GenerationId);
            var hashes = new List<string>();
            await using (var r = await complete.ExecuteReaderAsync(cancellationToken))
            {
                while (await r.ReadAsync(cancellationToken))
                {
                    if (r.IsDBNull(1))
                    {
                        throw new KnowledgeException("INVALID_STATE");
                    }

                    var values = r.GetFieldValue<Vector>(1).ToArray();
                    VectorProcessing.ValidateVector(values, job.Profile.EmbeddingDimensions);
                    hashes.Add(r.GetString(0) + ":" + ContentHash.Compute(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))).Value);
                }
            }

            if (hashes.Count == 0)
            {
                throw new KnowledgeException("INVALID_STATE");
            }

            manifest = ContentHash.Compute(Encoding.UTF8.GetBytes(string.Join('\n', hashes)));
        }
        var actor = await FinishVectorStateAsync(connection, transaction, generation, failure, at, manifest, cancellationToken);
        if (failure is null)
        {
            // Texto e vetores concluídos: publicar sem aprovação manual.
            await KnowledgePublication.PublishAsync(connection, transaction, generation.Finish("succeeded", at, manifest, null), actor, at, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> FinishVectorStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, GeracaoProcessamento generation,
        string? failure, DateTimeOffset at, ContentHash? manifest, CancellationToken cancellationToken)
    {
        var finished = generation.Finish(failure is null ? "succeeded" : "failed", at, manifest, failure);
        var operation = await new OperationRepository(connection, transaction).GetForUpdateAsync(generation.OperationId, cancellationToken);
        // Esgotamento pode ocorrer antes de renovar o lease da última tentativa interrompida.
        if (operation.State == "queued")
        {
            operation = operation.StartPreparation(Guid.NewGuid(), at, TimeSpan.FromSeconds(1));
        }

        operation = operation.FinishWork(failure, at);
        await using var command = new NpgsqlCommand("""
            UPDATE rag.generations SET embedding_state=@embedding,state=@state,completed_at=@at,content_manifest_hash=@manifest,
                failure_code=@failure,embedding_owner=NULL,embedding_until=NULL WHERE generation_id=@id;
            UPDATE rag.operations SET state=@state,started_at=@started,finished_at=@at,lease_owner=NULL,lease_until=NULL,result_code=@failure WHERE operation_id=@operation
            """, connection, transaction);
        command.Parameters.AddWithValue("embedding", failure is null ? "ready" : "failed");
        command.Parameters.AddWithValue("state", finished.State);
        command.Parameters.AddWithValue("at", at);
        SqlParameters.Add(command, "manifest", NpgsqlDbType.Text, finished.ContentManifestHash?.Value);
        SqlParameters.Add(command, "failure", NpgsqlDbType.Text, failure);
        command.Parameters.AddWithValue("id", generation.GenerationId);
        command.Parameters.AddWithValue("operation", operation.OperationId);
        command.Parameters.AddWithValue("started", operation.StartedAt!.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await AuditAsync(connection, transaction, operation.PrincipalId, null, "ingestion.embeddings", generation.GenerationId, failure ?? "succeeded", at, operation.CorrelationId, cancellationToken);
        return operation.PrincipalId;
    }
}
