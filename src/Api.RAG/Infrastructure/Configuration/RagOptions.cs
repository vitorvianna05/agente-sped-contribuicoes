namespace Api.RAG.Infrastructure.Configuration;

internal sealed class RagOptions
{
    public string? ConfigurationPurpose { get; set; }
    public ConnectionOptions ConnectionStrings { get; set; } = new();
    public AuthenticationOptions Authentication { get; set; } = new();
    public ProcessingOptions Processing { get; set; } = new();
    public RetrievalOptions Retrieval { get; set; } = new();
    public EmbeddingOptions Embeddings { get; set; } = new();
    public RetentionOptions Retention { get; set; } = new();
}

internal sealed class ConnectionOptions
{
    public string? Rag { get; set; }
    public string? RagMigration { get; set; }
}

internal sealed class AuthenticationOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? Resource { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
    public string[] AllowedOrigins { get; set; } = [];
    public string[] AllowedHosts { get; set; } = [];
    public CentralAssignment[] CentralPermissionAssignments { get; set; } = [];
}

internal sealed class CentralAssignment
{
    public string PrincipalId { get; set; } = "";
    public string[] Permissions { get; set; } = [];
}

internal sealed class ProcessingOptions
{
    public long? MaxFileBytes { get; set; }
    public int? MaxPages { get; set; }
    public int? MaxConcurrentJobs { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? LeaseSeconds { get; set; }
    public ProcessingProfile[] Profiles { get; set; } = [];
}

internal sealed class ProcessingProfile
{
    public string Id { get; set; } = "";
    public string AdoptionState { get; set; } = "candidate";
    public string EmbeddingProvider { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public int Dimensions { get; set; }
    public string Metric { get; set; } = "";
}

internal sealed class RetrievalOptions
{
    public string? AdoptedProfileId { get; set; }
    public int? MaxQueryCharacters { get; set; }
    public int? TimeoutSeconds { get; set; }
}

internal sealed class EmbeddingOptions
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? Dimensions { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? MaxAttempts { get; set; }
    public int? RetryDelayMilliseconds { get; set; }
    public bool SdkAutomaticRetriesEnabled { get; set; }
}

internal sealed class RetentionOptions
{
    public string? PolicyVersion { get; set; }
    public string? ApprovalReference { get; set; }
    public int? WithdrawnSourceDays { get; set; }
    public int? SupersededGenerationDays { get; set; }
    public int? OriginalDays { get; set; }
    public int? ReferenceDays { get; set; }
    public int? TombstoneDays { get; set; }
    public int? AuditDays { get; set; }
    public int? QueryDays { get; set; }
    public int? ConsumptionDays { get; set; }
    public bool AutomaticExpurgeEnabled { get; set; }
}
