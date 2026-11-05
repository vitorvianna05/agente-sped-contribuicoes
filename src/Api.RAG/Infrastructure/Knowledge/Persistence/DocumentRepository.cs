using System.Text;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed class DocumentRepository(RagDataSource source) : IDocumentStore
{
    private static readonly JsonSerializerOptions Json = IngestionMetadata.JsonOptions;

    public async Task<OperationSnapshot> SubmitAsync(IngestionSubmission submission, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var operation = submission.Operation;
        var metadata = submission.Metadata;
        metadata.Validate();
        submission.Profile.Validate();
        operation.Validate();
        if (operation.DocumentVersionId is null || operation.GenerationId is null || operation.Type != "ingestion")
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        var hash = ContentHash.Compute(submission.Original);
        // Ordem única de locks também serializa submissões concorrentes da mesma identidade oficial.
        await using (var mutex = Command("SELECT pg_advisory_xact_lock(hashtextextended(@identity,0))"))
        {
            mutex.Parameters.AddWithValue("identity", JsonSerializer.Serialize(new[] { metadata.Issuer, metadata.OfficialIdentifier }));
            await mutex.ExecuteNonQueryAsync(cancellationToken);
        }
        var registration = await new OperationRepository(connection, transaction).RegisterAsync(operation, cancellationToken);
        if (!registration.Created)
        {
            await transaction.CommitAsync(cancellationToken);
            return await GetOperationAsync(registration.Operation.OperationId, cancellationToken);
        }
        var definition = JsonSerializer.Serialize(submission.Profile, Json);
        var profileHash = ContentHash.Compute(Encoding.UTF8.GetBytes(definition)).Value;
        await using (var profile = Command("""
            INSERT INTO rag.processing_profiles VALUES (@id,@definition,@hash) ON CONFLICT DO NOTHING;
            SELECT definition_hash FROM rag.processing_profiles WHERE profile_id=@id
            """))
        {
            profile.Parameters.AddWithValue("id", submission.Profile.ProfileId);
            profile.Parameters.AddWithValue("definition", NpgsqlDbType.Jsonb, definition);
            profile.Parameters.AddWithValue("hash", profileHash);
            if (!Equals(await profile.ExecuteScalarAsync(cancellationToken), profileHash))
            {
                throw new KnowledgeException("PROFILE_INCOMPATIBLE");
            }
        }
        Guid sourceId;
        await using (var documentSource = Command("""
            INSERT INTO rag.sources VALUES (@id,@issuer,@identifier,@title,@kind,@origin,@at) ON CONFLICT DO NOTHING;
            SELECT source_id,title,kind,origin_uri FROM rag.sources WHERE issuer=@issuer AND official_identifier=@identifier FOR UPDATE
            """))
        {
            documentSource.Parameters.AddWithValue("id", Guid.NewGuid());
            documentSource.Parameters.AddWithValue("issuer", metadata.Issuer);
            documentSource.Parameters.AddWithValue("identifier", metadata.OfficialIdentifier);
            documentSource.Parameters.AddWithValue("title", metadata.Title);
            documentSource.Parameters.AddWithValue("kind", metadata.Kind);
            documentSource.Parameters.AddWithValue("origin", metadata.OriginUri);
            documentSource.Parameters.AddWithValue("at", operation.CreatedAt);
            await using var reader = await documentSource.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            sourceId = reader.GetGuid(0);
            if (reader.GetString(1) != metadata.Title || reader.GetString(2) != metadata.Kind || reader.GetString(3) != metadata.OriginUri)
            {
                throw new KnowledgeException("SOURCE_METADATA_CONFLICT");
            }
        }
        var versionId = operation.DocumentVersionId!.Value;
        var generationId = operation.GenerationId!.Value;
        await using (var existing = Command("SELECT document_version_id,original_content_hash,metadata FROM rag.document_versions WHERE source_id=@source AND official_version=@version FOR UPDATE"))
        {
            existing.Parameters.AddWithValue("source", sourceId);
            existing.Parameters.AddWithValue("version", metadata.OfficialVersion);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                versionId = reader.GetGuid(0);
                VersaoDocumental.AssertSameContent(new ContentHash(reader.GetString(1)), hash);
                var previous = JsonSerializer.Deserialize<IngestionMetadata>(reader.GetString(2), Json)!;
                if (JsonSerializer.Serialize(previous with { ProfileId = metadata.ProfileId }, Json) != JsonSerializer.Serialize(metadata, Json))
                {
                    throw new KnowledgeException("SOURCE_METADATA_CONFLICT");
                }
            }
        }
        if (versionId == operation.DocumentVersionId)
        {
            var document = new VersaoDocumental(versionId, sourceId, metadata.OfficialVersion, hash, Guid.NewGuid(),
                "unpublished", null, null, null, null, Guid.NewGuid(), operation.CreatedAt);
            document.Validate();
            await using var version = Command("""
                INSERT INTO rag.document_versions (document_version_id,source_id,official_version,original_content_hash,
                    original_artifact_id,original,metadata,revision,created_at,retention_policy_version)
                VALUES (@id,@source,@version,@hash,@artifact,@original,@metadata,@revision,@at,@policy)
                """);
            version.Parameters.AddWithValue("id", versionId);
            version.Parameters.AddWithValue("source", sourceId);
            version.Parameters.AddWithValue("version", metadata.OfficialVersion);
            version.Parameters.AddWithValue("hash", hash.Value);
            version.Parameters.AddWithValue("artifact", document.OriginalArtifactId);
            version.Parameters.AddWithValue("original", submission.Original);
            version.Parameters.AddWithValue("metadata", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(metadata, Json));
            version.Parameters.AddWithValue("revision", document.Revision);
            version.Parameters.AddWithValue("at", operation.CreatedAt);
            version.Parameters.AddWithValue("policy", submission.RetentionPolicyVersion);
            await version.ExecuteNonQueryAsync(cancellationToken);
            foreach (var item in metadata.Applicabilities)
            {
                new Aplicabilidade(item.DocumentType, item.LayoutVersion, item.OfficialBasis, operation.PrincipalId, operation.CreatedAt).Validate();
                await using var applicability = Command("INSERT INTO rag.applicabilities VALUES (@id,@type,@layout,@basis,@actor,@at)");
                applicability.Parameters.AddWithValue("id", versionId);
                applicability.Parameters.AddWithValue("type", item.DocumentType);
                applicability.Parameters.AddWithValue("layout", item.LayoutVersion);
                applicability.Parameters.AddWithValue("basis", item.OfficialBasis);
                applicability.Parameters.AddWithValue("actor", operation.PrincipalId);
                applicability.Parameters.AddWithValue("at", operation.CreatedAt);
                await applicability.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        Guid? reusedOperation = null;
        await using (var generation = Command("SELECT generation_id,operation_id FROM rag.generations WHERE document_version_id=@id AND profile_id=@profile ORDER BY created_at,generation_id LIMIT 1"))
        {
            generation.Parameters.AddWithValue("id", versionId);
            generation.Parameters.AddWithValue("profile", submission.Profile.ProfileId);
            await using var reader = await generation.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                generationId = reader.GetGuid(0);
                reusedOperation = reader.GetGuid(1);
            }
        }
        if (reusedOperation is null)
        {
            new GeracaoProcessamento(generationId, versionId, operation.OperationId, submission.Profile.ProfileId, hash, "queued", operation.CreatedAt).Validate();
            await using var generation = Command("""
                INSERT INTO rag.generations (generation_id,document_version_id,operation_id,profile_id,source_hash,state,preparation_state,created_at)
                VALUES (@id,@document,@operation,@profile,@hash,'queued','queued',@at)
                """);
            generation.Parameters.AddWithValue("id", generationId);
            generation.Parameters.AddWithValue("document", versionId);
            generation.Parameters.AddWithValue("operation", operation.OperationId);
            generation.Parameters.AddWithValue("profile", submission.Profile.ProfileId);
            generation.Parameters.AddWithValue("hash", hash.Value);
            generation.Parameters.AddWithValue("at", operation.CreatedAt);
            await generation.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var update = Command("UPDATE rag.operations SET document_version_id=@document,generation_id=@generation WHERE operation_id=@id"))
        {
            update.Parameters.AddWithValue("id", operation.OperationId);
            update.Parameters.AddWithValue("document", versionId);
            update.Parameters.AddWithValue("generation", generationId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        await new KnowledgeAuditWriter(connection, transaction).WriteAsync(new AuditEntry(Guid.NewGuid(), operation.PrincipalId,
            submission.OrganizationId, "ingestion", versionId, reusedOperation is null ? "accepted" : "reused", operation.CreatedAt, operation.CorrelationId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetOperationAsync(operation.OperationId, cancellationToken);

        NpgsqlCommand Command(string sql) => new(sql, connection, transaction);
    }

    public async Task<OperationSnapshot> GetOperationAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        // Aliases de reingestão acompanham a operação canônica da mesma geração.
        await using var command = new NpgsqlCommand("""
            SELECT COALESCE(c.operation_id,o.operation_id),o.type,COALESCE(c.state,o.state),o.document_version_id,o.generation_id,COALESCE(c.created_at,o.created_at),
                COALESCE(c.started_at,o.started_at),COALESCE(c.finished_at,o.finished_at),COALESCE(c.result_code,o.result_code),g.preparation_state,
                COALESCE(c.attempt_number,o.attempt_number)
            FROM rag.operations o LEFT JOIN rag.generations g ON g.generation_id=o.generation_id
            LEFT JOIN rag.operations c ON c.operation_id=g.operation_id WHERE o.operation_id=@id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KnowledgeException("RESOURCE_NOT_FOUND");
        }
        var state = reader.GetString(2);
        var result = reader.IsDBNull(8) ? null : reader.GetString(8);
        var preparation = reader.IsDBNull(9) ? null : reader.GetString(9);
        return new OperationSnapshot(reader.GetGuid(0), reader.GetString(1), state, reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
            result, preparation, $"\"{reader.GetGuid(0):N}-{state}-{preparation}-{reader.GetInt32(10)}\"");
    }

    public async Task<DocumentSnapshot> GetDocumentAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT metadata,original_content_hash,publication_state,active_generation_id,revision,created_at FROM rag.document_versions WHERE document_version_id=@id", connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KnowledgeException("RESOURCE_NOT_FOUND");
        }
        return new DocumentSnapshot(id, JsonSerializer.Deserialize<IngestionMetadata>(reader.GetString(0), Json)!, reader.GetString(1),
            reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), $"\"{reader.GetGuid(4):N}\"", reader.GetFieldValue<DateTimeOffset>(5));
    }

    public async Task<PreparationJob?> ClaimAsync(DateTimeOffset now, TimeSpan lease, int maximumAttempts, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var claim = new NpgsqlCommand("""
            SELECT g.generation_id,g.document_version_id,g.operation_id,g.profile_id,g.source_hash,g.state,g.created_at,
                g.completed_at,g.superseded_at,g.content_manifest_hash,g.failure_code,g.preparation_state,g.lease_owner,g.lease_until,
                o.attempt_number,d.original,p.definition,o.principal_id,o.correlation_id
            FROM rag.generations g JOIN rag.document_versions d USING(document_version_id)
            JOIN rag.processing_profiles p USING(profile_id) JOIN rag.operations o ON o.operation_id=g.operation_id
            WHERE g.preparation_state='queued' OR (g.preparation_state='processing' AND g.lease_until<=@now)
            ORDER BY g.created_at FOR UPDATE OF g SKIP LOCKED LIMIT 1
            """, connection, transaction);
        claim.Parameters.AddWithValue("now", now);
        PreparationJob job;
        GeracaoProcessamento generation;
        await using (var reader = await claim.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            generation = ReadGeneration(reader);
            job = new PreparationJob(generation.GenerationId, generation.OperationId, reader.GetFieldValue<byte[]>(15),
                JsonSerializer.Deserialize<PerfilProcessamento>(reader.GetString(16), Json)!, Guid.NewGuid(), reader.GetString(17), reader.GetString(18));
        }
        var operation = await new OperationRepository(connection, transaction).GetForUpdateAsync(job.OperationId, cancellationToken);
        var claimed = generation.ClaimPreparation(job.LeaseOwner, now, lease, maximumAttempts);
        var nextOperation = claimed.PreparationState == "failed"
            ? operation.CompletePreparation(claimed.FailureCode, now)
            : operation.StartPreparation(job.LeaseOwner, now, lease);
        await PersistPreparationAsync(connection, transaction, claimed, nextOperation, null, cancellationToken);
        await new KnowledgeAuditWriter(connection, transaction).WriteAsync(new AuditEntry(Guid.NewGuid(), operation.PrincipalId, null,
            "ingestion.claim", generation.GenerationId, claimed.FailureCode ?? "claimed", now, operation.CorrelationId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed.PreparationState == "failed" ? null : job;
    }

    public async Task FinishPreparationAsync(PreparationJob job, IReadOnlyList<Trecho> chunks, string? failureCode,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        GeracaoProcessamento generation;
        await using (var check = new NpgsqlCommand("""
            SELECT g.generation_id,g.document_version_id,g.operation_id,g.profile_id,g.source_hash,g.state,g.created_at,
                g.completed_at,g.superseded_at,g.content_manifest_hash,g.failure_code,g.preparation_state,g.lease_owner,g.lease_until,o.attempt_number
            FROM rag.generations g JOIN rag.operations o ON o.operation_id=g.operation_id WHERE g.generation_id=@id FOR UPDATE OF g
            """, connection, transaction))
        {
            check.Parameters.AddWithValue("id", job.GenerationId);
            await using var reader = await check.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new KnowledgeException("RESOURCE_NOT_FOUND");
            }
            generation = ReadGeneration(reader);
        }
        if (generation.OperationId != job.OperationId)
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }
        var manifest = failureCode is null ? ContentHash.Compute(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(chunks, Json))) : null;
        var completed = generation.CompletePreparation(job.LeaseOwner, at, manifest, failureCode);
        var operation = await new OperationRepository(connection, transaction).GetForUpdateAsync(job.OperationId, cancellationToken);
        var completedOperation = operation.CompletePreparation(failureCode, at);
        if (failureCode is null)
        {
            if (chunks.Count == 0 || chunks.Select(chunk => chunk.Ordinal).Where((ordinal, index) => ordinal != index).Any())
            {
                throw new KnowledgeException("INVALID_STATE");
            }
            foreach (var chunk in chunks)
            {
                chunk.Validate();
                if (chunk.GenerationId != job.GenerationId)
                {
                    throw new KnowledgeException("INVALID_STATE");
                }
                await using var insert = new NpgsqlCommand("INSERT INTO rag.chunks VALUES (@id,@generation,@ordinal,@text,@hash,@spans,@location,@reference,@at)", connection, transaction);
                insert.Parameters.AddWithValue("id", chunk.ChunkId);
                insert.Parameters.AddWithValue("generation", job.GenerationId);
                insert.Parameters.AddWithValue("ordinal", chunk.Ordinal);
                insert.Parameters.AddWithValue("text", chunk.Text);
                insert.Parameters.AddWithValue("hash", chunk.TextHash.Value);
                insert.Parameters.AddWithValue("spans", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(chunk.SourceSpans, Json));
                insert.Parameters.AddWithValue("location", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(chunk.Location, Json));
                insert.Parameters.AddWithValue("reference", chunk.ReferenceId);
                insert.Parameters.AddWithValue("at", chunk.CreatedAt);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await PersistPreparationAsync(connection, transaction, completed, completedOperation, manifest, cancellationToken);
        await new KnowledgeAuditWriter(connection, transaction).WriteAsync(new AuditEntry(Guid.NewGuid(), job.PrincipalId, null,
            "ingestion.prepare", job.GenerationId, failureCode ?? "text_ready", at, job.CorrelationId), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    private static GeracaoProcessamento ReadGeneration(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), new ContentHash(reader.GetString(4)),
        reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
        reader.IsDBNull(9) ? null : new ContentHash(reader.GetString(9)),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11),
        reader.IsDBNull(12) ? null : reader.GetGuid(12), reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13), reader.GetInt32(14));

    private static async Task PersistPreparationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        GeracaoProcessamento generation, Operacao operation, ContentHash? manifest, CancellationToken cancellationToken)
    {
        generation.Validate();
        operation.Validate();
        await using var update = new NpgsqlCommand("""
            UPDATE rag.generations SET preparation_state=@preparation,state=@generationState,lease_owner=@generationOwner,lease_until=@lease,
                failure_code=@failure,completed_at=@completed,preparation_manifest_hash=@manifest WHERE generation_id=@id;
            UPDATE rag.operations SET state=@state,started_at=@started,lease_owner=@operationOwner,lease_until=@lease,
                result_code=@result,finished_at=@finished,attempt_number=@attempt WHERE operation_id=@operation
            """, connection, transaction);
        update.Parameters.AddWithValue("preparation", generation.PreparationState);
        update.Parameters.AddWithValue("generationState", generation.State);
        SqlParameters.Add(update, "generationOwner", NpgsqlDbType.Uuid, generation.PreparationLeaseOwner);
        SqlParameters.Add(update, "lease", NpgsqlDbType.TimestampTz, generation.PreparationLeaseUntil);
        SqlParameters.Add(update, "failure", NpgsqlDbType.Text, generation.FailureCode);
        SqlParameters.Add(update, "completed", NpgsqlDbType.TimestampTz, generation.CompletedAt);
        SqlParameters.Add(update, "manifest", NpgsqlDbType.Text, manifest?.Value);
        update.Parameters.AddWithValue("id", generation.GenerationId);
        update.Parameters.AddWithValue("state", operation.State);
        SqlParameters.Add(update, "started", NpgsqlDbType.TimestampTz, operation.StartedAt);
        SqlParameters.Add(update, "operationOwner", NpgsqlDbType.Text, operation.LeaseOwner);
        SqlParameters.Add(update, "result", NpgsqlDbType.Text, operation.ResultCode);
        SqlParameters.Add(update, "finished", NpgsqlDbType.TimestampTz, operation.FinishedAt);
        update.Parameters.AddWithValue("attempt", operation.AttemptNumber);
        update.Parameters.AddWithValue("operation", operation.OperationId);
        await update.ExecuteNonQueryAsync(cancellationToken);
    }
}
