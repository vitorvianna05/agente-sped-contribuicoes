namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record AuditEntry(Guid AuditId, string PrincipalId, string? OrganizationId, string Action,
    Guid? ObjectId, string Result, DateTimeOffset OccurredAt, string CorrelationId);

internal interface IAuditWriter
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken);
}
