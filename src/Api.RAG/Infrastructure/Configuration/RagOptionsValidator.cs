using Api.RAG.Domain.Knowledge;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Configuration;

internal sealed class RagOptionsValidator(bool allowSynthetic = false) : IValidateOptions<RagOptions>
{
    public ValidateOptionsResult Validate(string? name, RagOptions options)
    {
        var errors = new List<string>();
        if (options.Processing.MaxFileBytes is not > 0 || options.Processing.MaxPages is not > 0 ||
            options.Processing.MaxConcurrentJobs is not > 0 || options.Processing.TimeoutSeconds is not > 0 ||
            options.Processing.LeaseSeconds is not > 0 || options.Retrieval.MaxQueryCharacters is not > 0 ||
            options.Retrieval.TimeoutSeconds is not > 0 || options.Embeddings.TimeoutSeconds is not > 0 ||
            options.Embeddings.MaxAttempts is not > 0 || options.Embeddings.RetryDelayMilliseconds is not >= 0)
        {
            errors.Add("Limites, timeout, tentativas e lease devem ser finitos e explícitos.");
        }

        if (!Uri.TryCreate(options.Authentication.Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(options.Authentication.Audience) ||
            !Uri.TryCreate(options.Authentication.Resource, UriKind.Absolute, out var resource) ||
            resource.Scheme != Uri.UriSchemeHttps || !options.Authentication.RequireHttpsMetadata ||
            options.Authentication.AllowedHosts.Length == 0 ||
            options.Authentication.AllowedHosts.Any(host => string.IsNullOrWhiteSpace(host) || host.Contains('*', StringComparison.Ordinal)) ||
            options.Authentication.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Identidade, recurso HTTPS e hosts permitidos devem ser explícitos.");
        }

        if (options.Embeddings.Dimensions is not > 0 || string.IsNullOrWhiteSpace(options.Embeddings.Provider) ||
            string.IsNullOrWhiteSpace(options.Embeddings.Model) || options.Embeddings.SdkAutomaticRetriesEnabled)
        {
            errors.Add("Embedding requer modelo/dimensão e tentativas controladas sem retries ocultos.");
        }

        if (options.Embeddings.Provider == "Fake" && (!allowSynthetic || options.ConfigurationPurpose != "SyntheticTestsOnly_NotApprovedForProduction"))
        {
            errors.Add("Provider Fake é exclusivo dos testes sintéticos.");
        }

        if (options.Processing.Profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count() != options.Processing.Profiles.Length)
        {
            errors.Add("Identificadores de perfis devem ser únicos.");
        }

        foreach (var profile in options.Processing.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || profile.AdoptionState is not ("candidate" or "adopted") ||
                profile.Dimensions <= 0 || profile.Metric != "cosine" ||
                string.IsNullOrWhiteSpace(profile.EmbeddingProvider) || string.IsNullOrWhiteSpace(profile.EmbeddingModel))
            {
                errors.Add("Perfil de processamento inválido.");
            }
        }

        var retention = options.Retention;
        if (new[] { retention.WithdrawnSourceDays, retention.SupersededGenerationDays, retention.OriginalDays,
            retention.ReferenceDays, retention.TombstoneDays, retention.AuditDays, retention.QueryDays, retention.ConsumptionDays }.Any(value => value is <= 0) ||
            retention.AutomaticExpurgeEnabled)
        {
            errors.Add("Retenção ausente permanece pendente; prazo definido deve ser positivo e expurgo manual.");
        }

        if (!string.IsNullOrWhiteSpace(options.ConnectionStrings.Rag))
        {
            try
            {
                var connection = new Npgsql.NpgsqlConnectionStringBuilder(options.ConnectionStrings.Rag);
                if (string.IsNullOrWhiteSpace(connection.Host) || string.IsNullOrWhiteSpace(connection.Database))
                {
                    errors.Add("Conexão da Api.RAG incompleta.");
                }
            }
            catch (ArgumentException)
            {
                errors.Add("Conexão da Api.RAG inválida.");
            }
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public static void RequireExpurge(RagOptions options, bool synthetic = false)
    {
        var retention = options.Retention;
        if (string.IsNullOrWhiteSpace(retention.PolicyVersion) ||
            (!synthetic && string.IsNullOrWhiteSpace(retention.ApprovalReference)) || retention.AutomaticExpurgeEnabled ||
            new[] { retention.WithdrawnSourceDays, retention.SupersededGenerationDays, retention.OriginalDays,
                retention.ReferenceDays, retention.TombstoneDays }.Any(value => value is not > 0))
        {
            throw new KnowledgeException("RETENTION_NOT_CONFIGURED");
        }
    }

    public static void RequireOperationalProfile(RagOptions options)
    {
        var profile = options.Processing.Profiles.SingleOrDefault(profile => profile.Id == options.Retrieval.AdoptedProfileId);
        if (profile is null || profile.AdoptionState != "adopted" ||
            profile.EmbeddingProvider != options.Embeddings.Provider || profile.EmbeddingModel != options.Embeddings.Model ||
            profile.Dimensions != options.Embeddings.Dimensions)
        {
            throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        }
    }

    public static void RequirePersistence(RagOptions options, bool synthetic)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionStrings.Rag))
        {
            throw new KnowledgeException("DEPENDENCY_UNAVAILABLE");
        }

        if (string.IsNullOrWhiteSpace(options.Retention.PolicyVersion) ||
            (!synthetic && string.IsNullOrWhiteSpace(options.Retention.ApprovalReference)) ||
            options.Retention.AuditDays is not > 0 || options.Retention.QueryDays is not > 0 ||
            options.Retention.ConsumptionDays is not > 0)
        {
            throw new KnowledgeException("RETENTION_NOT_CONFIGURED");
        }
    }
}
