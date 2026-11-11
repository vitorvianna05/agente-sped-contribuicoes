using System.Data;
using System.Text.Json;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Npgsql;
using Pgvector;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed partial class KnowledgeRepository
{
    public async Task RequireProfileAsync(PerfilProcessamento profile, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var select = new NpgsqlCommand("SELECT definition FROM rag.processing_profiles WHERE profile_id=@id", connection);
        select.Parameters.AddWithValue("id", profile.ProfileId);
        if (await select.ExecuteScalarAsync(cancellationToken) is not string definition || JsonSerializer.Deserialize<PerfilProcessamento>(definition, Json) != profile)
        {
            throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        }
    }
    private const string EvidenceColumns = """
        c.reference_id,s.source_id,d.document_version_id,g.generation_id,c.chunk_id,s.issuer,s.official_identifier,
        d.official_version,d.metadata->>'originUri',c.text,a.document_type,a.layout_version,a.official_basis,c.location,
        CASE WHEN d.publication_state='withdrawn' THEN 'withdrawn' WHEN d.active_generation_id=g.generation_id THEN 'active' ELSE 'superseded' END
        """;

    private static RetrievedEvidence Evidence(NpgsqlDataReader r, double? score) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), r.GetGuid(4),
        r.GetString(5), r.GetString(6), r.GetString(7), new Uri(r.GetString(8)), r.GetString(9), r.GetString(10), r.GetString(11), r.GetString(12),
        JsonSerializer.Deserialize<ValuesLocation>(r.GetString(13), Json)!, r.GetString(14), score);

    // Candidatos por modalidade e constante k da Reciprocal Rank Fusion (valor usual da literatura).
    private const int CandidatePool = 50;
    private const int FusionK = 60;

    public async Task<RetrievedEvidence[]> SearchAsync(float[] vector, string query, PerfilProcessamento profile, string type, string layout, CancellationToken cancellationToken)
    {
        VectorProcessing.ValidateVector(vector, profile.EmbeddingDimensions);
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        // Busca híbrida: os mesmos filtros de elegibilidade alimentam um ranking vetorial (cosseno) e um
        // textual (tsvector português, termos da consulta em OU); a ordem final é a soma RRF dos dois.
        // Códigos como "0000" ou "M200" são mal representados em embeddings e bem resolvidos pelo índice textual.
        await using var command = new NpgsqlCommand($"""
            WITH eligible AS (
                SELECT c.chunk_id, c.reference_id, c.embedding <=> @vector AS distance, c.text_search
                FROM rag.chunks c JOIN rag.generations g USING(generation_id) JOIN rag.document_versions d USING(document_version_id)
                JOIN rag.applicabilities a ON a.document_version_id=d.document_version_id
                WHERE d.publication_state='published' AND d.active_generation_id=g.generation_id AND g.state='succeeded'
                  AND g.embedding_state='ready' AND g.profile_id=@profile AND a.document_type=@type AND a.layout_version=@layout
                  AND c.embedding IS NOT NULL AND c.text IS NOT NULL AND 1-(c.embedding <=> @vector)>=@threshold
            ),
            terms AS (
                SELECT NULLIF(replace(plainto_tsquery('portuguese', @query)::text, ' & ', ' | '), '')::tsquery AS q
            ),
            vector_rank AS (
                SELECT chunk_id, row_number() OVER (ORDER BY distance, reference_id) AS position
                FROM eligible ORDER BY distance, reference_id LIMIT @pool
            ),
            text_rank AS (
                SELECT e.chunk_id, row_number() OVER (ORDER BY ts_rank_cd(e.text_search, t.q) DESC, e.reference_id) AS position
                FROM eligible e CROSS JOIN terms t WHERE t.q IS NOT NULL AND e.text_search @@ t.q
                ORDER BY ts_rank_cd(e.text_search, t.q) DESC, e.reference_id LIMIT @pool
            ),
            fused AS (
                SELECT chunk_id, sum(1.0 / (@k + position)) AS fusion
                FROM (SELECT * FROM vector_rank UNION ALL SELECT * FROM text_rank) ranked GROUP BY chunk_id
            )
            SELECT {EvidenceColumns},1-(c.embedding <=> @vector) AS score
            FROM fused f JOIN rag.chunks c USING(chunk_id) JOIN rag.generations g USING(generation_id)
            JOIN rag.document_versions d USING(document_version_id) JOIN rag.sources s USING(source_id)
            JOIN rag.applicabilities a ON a.document_version_id=d.document_version_id AND a.document_type=@type AND a.layout_version=@layout
            ORDER BY f.fusion DESC, c.reference_id LIMIT @limit
            """, connection, transaction);
        command.Parameters.AddWithValue("query", query);
        command.Parameters.AddWithValue("pool", CandidatePool);
        command.Parameters.AddWithValue("k", FusionK);
        command.Parameters.AddWithValue("vector", new Vector(vector));
        command.Parameters.AddWithValue("profile", profile.ProfileId);
        command.Parameters.AddWithValue("type", type);
        command.Parameters.AddWithValue("layout", layout);
        // Sem Threshold configurado, nenhum corte de similaridade; sem TopK, 5 resultados.
        command.Parameters.AddWithValue("threshold", profile.Threshold ?? -1d);
        command.Parameters.AddWithValue("limit", profile.TopK ?? 5);
        var found = new List<RetrievedEvidence>();
        await using (var r = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await r.ReadAsync(cancellationToken))
            {
                found.Add(Evidence(r, r.GetDouble(15)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return found.ToArray();
    }

    public async Task<(RetrievedEvidence? Evidence, bool Purged)> ResolveAsync(Guid referenceId, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await using (var tombstone = new NpgsqlCommand("SELECT reference_id FROM rag.reference_tombstones WHERE reference_id=@id AND expires_at>@at", connection, transaction))
        {
            tombstone.Parameters.AddWithValue("id", referenceId);
            tombstone.Parameters.AddWithValue("at", clock.GetUtcNow());
            if (await tombstone.ExecuteScalarAsync(cancellationToken) is Guid)
            {
                return (null, true);
            }
        }
        await using var command = new NpgsqlCommand($"""
            SELECT {EvidenceColumns} FROM rag.chunks c JOIN rag.generations g USING(generation_id)
            JOIN rag.document_versions d USING(document_version_id) JOIN rag.sources s USING(source_id)
            JOIN rag.applicabilities a ON a.document_version_id=d.document_version_id
            WHERE c.reference_id=@id AND c.text IS NOT NULL AND g.state='succeeded'
              AND (g.generation_id=d.active_generation_id OR g.superseded_at IS NOT NULL OR d.withdrawn_at IS NOT NULL)
              AND g.activated_at IS NOT NULL
            ORDER BY a.document_type,a.layout_version LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue("id", referenceId);
        RetrievedEvidence? evidence;
        await using (var r = await command.ExecuteReaderAsync(cancellationToken))
        {
            evidence = await r.ReadAsync(cancellationToken) ? Evidence(r, null) : null;
        }

        await transaction.CommitAsync(cancellationToken);
        return (evidence, false);
    }

    public async Task<Guid> BeginQueryAsync(string principal, string? organization, string correlation, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var operation = new Operacao
        {
            OperationId = id,
            Type = "query",
            PrincipalId = principal,
            CorrelationId = correlation,
            State = "queued",
            CreatedAt = at,
            IdempotencyKeyDigest = ContentHash.Compute(id.ToByteArray()),
            RequestDigest = ContentHash.Compute(id.ToByteArray())
        };
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await new OperationRepository(connection, transaction).RegisterAsync(operation, cancellationToken);
        await AuditAsync(connection, transaction, principal, organization, "knowledge.query", id, "started", at, correlation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    public async Task AuditQueryAsync(Guid queryId, string principal, string? organization, string correlation, string result, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var operation = await new OperationRepository(connection, transaction).GetForUpdateAsync(queryId, cancellationToken);
        operation = operation.StartPreparation(Guid.NewGuid(), at, TimeSpan.FromSeconds(1)).FinishWork(result == "technical_failure" ? "TECHNICAL_FAILURE" : null, at, result == "cancelled");
        await using var update = new NpgsqlCommand("UPDATE rag.operations SET state=@state,started_at=@at,finished_at=@at,result_code=@result WHERE operation_id=@id", connection, transaction);
        update.Parameters.AddWithValue("state", operation.State);
        update.Parameters.AddWithValue("at", at);
        update.Parameters.AddWithValue("result", result);
        update.Parameters.AddWithValue("id", queryId);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await AuditAsync(connection, transaction, principal, organization, "knowledge.query", queryId, result, at, correlation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
