using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace Api.RAG.Infrastructure.Knowledge.Observability;

internal sealed class KnowledgeAuditWriter(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAuditWriter
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.PrincipalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.CorrelationId);
        await using var command = new NpgsqlCommand("""
            INSERT INTO rag.audit_entries(audit_id,principal_id,organization_id,action,object_id,result,occurred_at,correlation_id)
            VALUES (@id,@principal,@organization,@action,@object,@result,@occurred,@correlation)
            """, connection, transaction);
        SqlParameters.Add(command, "id", NpgsqlDbType.Uuid, entry.AuditId);
        SqlParameters.Add(command, "principal", NpgsqlDbType.Text, entry.PrincipalId);
        SqlParameters.Add(command, "organization", NpgsqlDbType.Text, entry.OrganizationId);
        SqlParameters.Add(command, "action", NpgsqlDbType.Text, entry.Action);
        SqlParameters.Add(command, "object", NpgsqlDbType.Uuid, entry.ObjectId);
        SqlParameters.Add(command, "result", NpgsqlDbType.Text, entry.Result);
        SqlParameters.Add(command, "occurred", NpgsqlDbType.TimestampTz, entry.OccurredAt);
        SqlParameters.Add(command, "correlation", NpgsqlDbType.Text, entry.CorrelationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
