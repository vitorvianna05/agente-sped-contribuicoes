using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Domain.Knowledge.Values;
using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Persistence;

internal sealed class OperationRepository(NpgsqlConnection connection, NpgsqlTransaction transaction) : IOperationRepository
{
    public async Task<(Operacao Operation, bool Created)> RegisterAsync(Operacao operation, CancellationToken cancellationToken)
    {
        operation.Validate();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO rag.operations (operation_id,type,document_version_id,generation_id,principal_id,correlation_id,
                state,created_at,started_at,finished_at,lease_until,lease_owner,attempt_number,result_code,idempotency_key_digest,request_digest)
            VALUES (@id,@type,@document,@generation,@principal,@correlation,@state,@created,@started,@finished,@lease,@owner,@attempt,@result,@key,@digest)
            ON CONFLICT (principal_id,type,idempotency_key_digest) DO NOTHING RETURNING operation_id
            """, connection, transaction);
        SqlParameters.Add(insert, "id", NpgsqlDbType.Uuid, operation.OperationId);
        SqlParameters.Add(insert, "type", NpgsqlDbType.Text, operation.Type);
        SqlParameters.Add(insert, "document", NpgsqlDbType.Uuid, operation.DocumentVersionId);
        SqlParameters.Add(insert, "generation", NpgsqlDbType.Uuid, operation.GenerationId);
        SqlParameters.Add(insert, "principal", NpgsqlDbType.Text, operation.PrincipalId);
        SqlParameters.Add(insert, "correlation", NpgsqlDbType.Text, operation.CorrelationId);
        SqlParameters.Add(insert, "state", NpgsqlDbType.Text, operation.State);
        SqlParameters.Add(insert, "created", NpgsqlDbType.TimestampTz, operation.CreatedAt);
        SqlParameters.Add(insert, "started", NpgsqlDbType.TimestampTz, operation.StartedAt);
        SqlParameters.Add(insert, "finished", NpgsqlDbType.TimestampTz, operation.FinishedAt);
        SqlParameters.Add(insert, "lease", NpgsqlDbType.TimestampTz, operation.LeaseUntil);
        SqlParameters.Add(insert, "owner", NpgsqlDbType.Text, operation.LeaseOwner);
        SqlParameters.Add(insert, "attempt", NpgsqlDbType.Integer, operation.AttemptNumber);
        SqlParameters.Add(insert, "result", NpgsqlDbType.Text, operation.ResultCode);
        SqlParameters.Add(insert, "key", NpgsqlDbType.Text, operation.IdempotencyKeyDigest.Value);
        SqlParameters.Add(insert, "digest", NpgsqlDbType.Text, operation.RequestDigest.Value);
        if (await insert.ExecuteScalarAsync(cancellationToken) is Guid)
        {
            return (operation, true);
        }

        await using var select = new NpgsqlCommand("""
            SELECT operation_id,type,document_version_id,generation_id,principal_id,correlation_id,state,created_at,
                started_at,finished_at,lease_until,lease_owner,attempt_number,result_code,idempotency_key_digest,request_digest
            FROM rag.operations WHERE principal_id=@principal AND type=@type AND idempotency_key_digest=@key
            FOR UPDATE
            """, connection, transaction);
        select.Parameters.AddWithValue("principal", operation.PrincipalId);
        select.Parameters.AddWithValue("type", operation.Type);
        select.Parameters.AddWithValue("key", operation.IdempotencyKeyDigest.Value);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KnowledgeException("DEPENDENCY_UNAVAILABLE");
        }

        var existing = Read(reader);
        if (existing.RequestDigest != operation.RequestDigest)
        {
            throw new KnowledgeException("IDEMPOTENCY_CONFLICT");
        }

        return (existing, false);
    }

    internal async Task<Operacao> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT operation_id,type,document_version_id,generation_id,principal_id,correlation_id,state,created_at,
                started_at,finished_at,lease_until,lease_owner,attempt_number,result_code,idempotency_key_digest,request_digest
            FROM rag.operations WHERE operation_id=@id FOR UPDATE
            """, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new KnowledgeException("RESOURCE_NOT_FOUND");
        }
        var operation = Read(reader);
        operation.Validate();
        return operation;
    }

    private static Operacao Read(NpgsqlDataReader reader) => new()
    {
        OperationId = reader.GetGuid(0),
        Type = reader.GetString(1),
        DocumentVersionId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
        GenerationId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
        PrincipalId = reader.GetString(4),
        CorrelationId = reader.GetString(5),
        State = reader.GetString(6),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(7),
        StartedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
        FinishedAt = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
        LeaseUntil = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10),
        LeaseOwner = reader.IsDBNull(11) ? null : reader.GetString(11),
        AttemptNumber = reader.GetInt32(12),
        ResultCode = reader.IsDBNull(13) ? null : reader.GetString(13),
        IdempotencyKeyDigest = new ContentHash(reader.GetString(14)),
        RequestDigest = new ContentHash(reader.GetString(15))
    };
}
