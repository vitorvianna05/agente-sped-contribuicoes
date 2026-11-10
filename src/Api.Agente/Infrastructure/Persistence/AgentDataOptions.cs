namespace Api.Agente.Infrastructure.Persistence;

internal sealed class AgentDataOptions
{
    public string? ConnectionString { get; set; }
    public string? MigrationConnectionString { get; set; }
    public string? PolicyVersion { get; set; }
    public string? ApprovalReference { get; set; }
    public int? AuditDays { get; set; }
    public int? ConsumptionDays { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetentionBatchSize { get; set; }

    public bool HasApprovedPolicy => !string.IsNullOrWhiteSpace(PolicyVersion) && !string.IsNullOrWhiteSpace(ApprovalReference) &&
        AuditDays is > 0 && ConsumptionDays is > 0 && AuditDays >= ConsumptionDays;
}
