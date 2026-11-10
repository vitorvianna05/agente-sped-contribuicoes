using Api.RAG.Domain.Knowledge;

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
    public OpenAiOptions OpenAI { get; set; } = new();

    /// <summary>
    /// A seção OpenAI é a origem preferencial da chave e do modelo de embeddings. Valores explícitos em
    /// Embeddings prevalecem; divergência entre as duas seções é rejeitada pelo validador.
    /// </summary>
    public void ApplyOpenAiSection()
    {
        if (string.IsNullOrWhiteSpace(Embeddings.ApiKey) && !string.IsNullOrWhiteSpace(OpenAI.ApiKey))
        {
            Embeddings.ApiKey = OpenAI.ApiKey;
        }
        if (string.IsNullOrWhiteSpace(Embeddings.Model) && !string.IsNullOrWhiteSpace(OpenAI.EmbeddingModel))
        {
            Embeddings.Model = OpenAI.EmbeddingModel;
        }
    }

    /// <summary>Etapa vetorial: valor explícito ou, se omitido, ativa somente quando há chave do provedor.</summary>
    public bool EmbeddingStageActive => Processing.EmbeddingStageEnabled ??
        (Embeddings.Provider != "OpenAI" || !string.IsNullOrWhiteSpace(Embeddings.ApiKey));
}

internal sealed class OpenAiOptions
{
    public string? ApiKey { get; set; }
    /// <summary>Reservado à futura Api.Agente; a Api.RAG não chama modelos de chat.</summary>
    public string? ChatModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public OpenAiPricingOptions Pricing { get; set; } = new();
}

internal sealed class OpenAiPricingOptions
{
    public string? Currency { get; set; }
    /// <summary>Origem da tabela de preços registrada no consumo.</summary>
    public string? Reference { get; set; }
    /// <summary>Versão da tabela; quando omitida, deriva dos próprios valores configurados.</summary>
    public string? Version { get; set; }
    public ChatPricingOptions Chat { get; set; } = new();
    public EmbeddingPricingOptions Embedding { get; set; } = new();

    public ConsumptionPrice? ToEmbeddingPrice()
    {
        if (Embedding.InputPerMillionTokens is not { } perMillion || string.IsNullOrWhiteSpace(Currency))
        {
            return null;
        }
        var version = string.IsNullOrWhiteSpace(Version)
            ? "cfg-" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                string.Join('|', Currency, perMillion.ToString(System.Globalization.CultureInfo.InvariantCulture)))))[..12]
            : Version;
        return new ConsumptionPrice(perMillion, Currency, string.IsNullOrWhiteSpace(Reference) ? "configuration:OpenAI:Pricing" : Reference, version);
    }
}

internal sealed class ChatPricingOptions
{
    public decimal? InputPerMillionTokens { get; set; }
    public decimal? CachedInputPerMillionTokens { get; set; }
    public decimal? OutputPerMillionTokens { get; set; }
}

internal sealed class EmbeddingPricingOptions
{
    public decimal? InputPerMillionTokens { get; set; }
}

internal sealed class ConnectionOptions
{
    public string? Rag { get; set; }
    public string? RagMigration { get; set; }
}

internal sealed class AuthenticationOptions
{
    public bool DisabledForDevelopment { get; set; }
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
    public int? MaxPreparationAttempts { get; set; }
    /// <summary>true/false explícito; null ativa a etapa somente quando a chave do provedor está configurada.</summary>
    public bool? EmbeddingStageEnabled { get; set; }
    public string? ProfileApprovalReference { get; set; }
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
    public string? ExtractorVersion { get; set; }
    public string? NormalizationVersion { get; set; }
    public string? Segmenter { get; set; }
    public string? TokenizerVersion { get; set; }
    public int? ChunkTokens { get; set; }
    public int? OverlapTokens { get; set; }
    public string? QueryNormalizationVersion { get; set; }
    public int? TopK { get; set; }
    public double? Threshold { get; set; }
    public Guid? EvaluationId { get; set; }

    public PerfilProcessamento ToDomain() => new(Guid.TryParse(Id, out var id) ? id : Guid.Empty,
        ExtractorVersion ?? "", NormalizationVersion ?? "", Segmenter ?? "", ChunkTokens ?? 0, OverlapTokens ?? -1,
        TokenizerVersion ?? "", EmbeddingProvider, EmbeddingModel, Dimensions, Metric,
        QueryNormalizationVersion ?? "", TopK, Threshold, EvaluationId, AdoptionState);
}

internal sealed class RetrievalOptions
{
    public string? AdoptedProfileId { get; set; }
    public int? MaxQueryCharacters { get; set; }
    public int? TimeoutSeconds { get; set; }
}

internal sealed class EmbeddingOptions
{
    public string? ApiKey { get; set; }
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
