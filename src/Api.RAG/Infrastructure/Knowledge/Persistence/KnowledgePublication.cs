using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

/// <summary>Publicação automática da geração concluída, sem aprovação manual.</summary>
internal static class KnowledgePublication
{
    private static async Task<VersaoDocumental> LockDocumentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken token)
    {
        await using var select = new NpgsqlCommand("""
            SELECT document_version_id,source_id,official_version,original_content_hash,original_artifact_id,publication_state,
                active_generation_id,approved_by,approved_at,withdrawn_at,revision,created_at
            FROM rag.document_versions WHERE document_version_id=@id FOR UPDATE
            """, connection, transaction);
        select.Parameters.AddWithValue("id", id);
        await using var r = await select.ExecuteReaderAsync(token);
        if (!await r.ReadAsync(token))
        {
            throw new KnowledgeException("RESOURCE_NOT_FOUND");
        }

        return new VersaoDocumental(r.GetGuid(0), r.GetGuid(1), r.GetString(2), new ContentHash(r.GetString(3)), r.GetGuid(4), r.GetString(5),
            r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8),
            r.IsDBNull(9) ? null : r.GetFieldValue<DateTimeOffset>(9), r.GetGuid(10), r.GetFieldValue<DateTimeOffset>(11));
    }

    private static async Task SaveDocumentAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, VersaoDocumental document, CancellationToken token)
    {
        document.Validate();
        await using var update = new NpgsqlCommand("""
            UPDATE rag.document_versions SET publication_state=@state,active_generation_id=@active,approved_by=@actor,approved_at=@approved,
                withdrawn_at=@withdrawn,revision=@revision WHERE document_version_id=@id
            """, connection, transaction);
        update.Parameters.AddWithValue("state", document.PublicationState);
        SqlParameters.Add(update, "active", NpgsqlDbType.Uuid, document.ActiveGenerationId);
        SqlParameters.Add(update, "actor", NpgsqlDbType.Text, document.ApprovedBy);
        SqlParameters.Add(update, "approved", NpgsqlDbType.TimestampTz, document.ApprovedAt);
        SqlParameters.Add(update, "withdrawn", NpgsqlDbType.TimestampTz, document.WithdrawnAt);
        update.Parameters.AddWithValue("revision", document.Revision);
        update.Parameters.AddWithValue("id", document.DocumentVersionId);
        await update.ExecuteNonQueryAsync(token);
    }

    /// <summary>
    /// Torna a geração concluída a ativa da versão e retira da busca as demais versões publicadas da mesma fonte
    /// (a versão mais recente enviada substitui a anterior).
    /// </summary>
    public static async Task PublishAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, GeracaoProcessamento generation,
        string actor, DateTimeOffset at, CancellationToken token)
    {
        var document = await LockDocumentAsync(connection, transaction, generation.DocumentVersionId, token);
        var previous = document.ActiveGenerationId;
        document = document.PublishGeneration(generation, actor, at);
        await SaveDocumentAsync(connection, transaction, document, token);

        await using (var activate = new NpgsqlCommand("""
            UPDATE rag.generations SET activated_at=@at WHERE generation_id=@id;
            UPDATE rag.generations SET superseded_at=@at WHERE generation_id=@previous AND superseded_at IS NULL
            """, connection, transaction))
        {
            activate.Parameters.AddWithValue("at", at);
            activate.Parameters.AddWithValue("id", generation.GenerationId);
            SqlParameters.Add(activate, "previous", NpgsqlDbType.Uuid, previous is { } old && old != generation.GenerationId ? old : null);
            await activate.ExecuteNonQueryAsync(token);
        }

        var others = new List<Guid>();
        await using (var select = new NpgsqlCommand("""
            SELECT document_version_id FROM rag.document_versions
            WHERE source_id=@source AND document_version_id<>@id AND publication_state='published' ORDER BY document_version_id
            """, connection, transaction))
        {
            select.Parameters.AddWithValue("source", document.SourceId);
            select.Parameters.AddWithValue("id", document.DocumentVersionId);
            await using var r = await select.ExecuteReaderAsync(token);
            while (await r.ReadAsync(token))
            {
                others.Add(r.GetGuid(0));
            }
        }
        foreach (var id in others)
        {
            var replaced = (await LockDocumentAsync(connection, transaction, id, token)).Withdraw(at);
            await SaveDocumentAsync(connection, transaction, replaced, token);
        }
    }

    /// <summary>
    /// Reenvio de documento cuja geração já está concluída (texto e vetores) mas que não está publicado:
    /// publica essa geração. Cobre dados processados antes da publicação automática.
    /// </summary>
    public static async Task PublishCompletedIfPendingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid generationId,
        string actor, DateTimeOffset at, CancellationToken token)
    {
        GeracaoProcessamento generation;
        await using (var select = new NpgsqlCommand("""
            SELECT g.generation_id,g.document_version_id,g.operation_id,g.profile_id,g.source_hash,g.state,g.created_at,g.completed_at,
                g.superseded_at,g.content_manifest_hash,g.failure_code
            FROM rag.generations g JOIN rag.document_versions d USING(document_version_id)
            WHERE g.generation_id=@id AND g.state='succeeded' AND g.embedding_state='ready' AND g.superseded_at IS NULL
              AND d.publication_state IN ('unpublished','withdrawn') AND g.content_manifest_hash IS NOT NULL
            """, connection, transaction))
        {
            select.Parameters.AddWithValue("id", generationId);
            await using var r = await select.ExecuteReaderAsync(token);
            if (!await r.ReadAsync(token))
            {
                return;
            }

            generation = new GeracaoProcessamento(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetGuid(3), new ContentHash(r.GetString(4)), r.GetString(5),
                r.GetFieldValue<DateTimeOffset>(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7),
                r.IsDBNull(8) ? null : r.GetFieldValue<DateTimeOffset>(8), new ContentHash(r.GetString(9)), r.IsDBNull(10) ? null : r.GetString(10));
        }
        await PublishAsync(connection, transaction, generation, actor, at, token);
    }
}
