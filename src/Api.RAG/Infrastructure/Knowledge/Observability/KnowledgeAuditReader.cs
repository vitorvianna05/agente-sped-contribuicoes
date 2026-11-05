using System.Security.Claims;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace Api.RAG.Infrastructure.Knowledge.Observability;

internal sealed class KnowledgeAuditReader(RagDataSource source, IAuthorizationService authorization)
{
    public async Task<IReadOnlyList<AuditEntry>> ReadAsync(ClaimsPrincipal principal, string principalId, int limit, CancellationToken cancellationToken)
    {
        if (!(await authorization.AuthorizeAsync(principal, null, "knowledge.audit")).Succeeded)
        {
            throw new KnowledgeException("PERMISSION_DENIED");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        await using var connection = await source.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT audit_id,principal_id,organization_id,action,object_id,result,occurred_at,correlation_id
            FROM rag.audit_entries WHERE principal_id=@principal ORDER BY occurred_at DESC,audit_id LIMIT @limit
            """;
        command.Parameters.AddWithValue("principal", principalId);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var entries = new List<AuditEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AuditEntry(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7)));
        }

        return entries;
    }
}
