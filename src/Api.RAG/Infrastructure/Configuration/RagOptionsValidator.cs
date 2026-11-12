using Api.RAG.Domain.Knowledge;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Configuration;

internal sealed class RagOptionsValidator(bool allowSynthetic = false, bool allowDevelopment = false) : IValidateOptions<RagOptions>
{
    public ValidateOptionsResult Validate(string? name, RagOptions options)
    {
        var errors = new List<string>();
        (string Key, long? Value)[] requiredLimits =
        [
            ("Processing:MaxFileBytes", options.Processing.MaxFileBytes),
            ("Processing:MaxPages", options.Processing.MaxPages),
            ("Processing:MaxConcurrentJobs", options.Processing.MaxConcurrentJobs),
            ("Processing:TimeoutSeconds", options.Processing.TimeoutSeconds),
            ("Processing:LeaseSeconds", options.Processing.LeaseSeconds),
            ("Processing:MaxPreparationAttempts", options.Processing.MaxPreparationAttempts),
            ("Retrieval:MaxQueryCharacters", options.Retrieval.MaxQueryCharacters),
            ("Retrieval:TimeoutSeconds", options.Retrieval.TimeoutSeconds),
            ("Embeddings:TimeoutSeconds", options.Embeddings.TimeoutSeconds),
            ("Embeddings:MaxAttempts", options.Embeddings.MaxAttempts)
        ];
        var invalidLimits = requiredLimits.Where(limit => limit.Value is not > 0).Select(limit => limit.Key).ToList();
        if (options.Embeddings.RetryDelayMilliseconds is not >= 0)
        {
            invalidLimits.Add("Embeddings:RetryDelayMilliseconds");
        }
        if (invalidLimits.Count > 0)
        {
            errors.Add("Limites, timeout, tentativas e lease devem ser finitos e explícitos. Chaves ausentes ou inválidas: " + string.Join(", ", invalidLimits) + ".");
        }

        var localAuthentication = allowDevelopment && options.Authentication.DisabledForDevelopment;
        if (options.Authentication.DisabledForDevelopment && !allowDevelopment)
        {
            errors.Add("Authentication:DisabledForDevelopment exige ambiente Development.");
        }

        if ((!localAuthentication && (!Uri.TryCreate(options.Authentication.Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(options.Authentication.Audience))) ||
            !Uri.TryCreate(options.Authentication.Resource, UriKind.Absolute, out var resource) ||
            resource.Scheme != Uri.UriSchemeHttps || !options.Authentication.RequireHttpsMetadata ||
            options.Authentication.AllowedHosts.Length == 0 ||
            options.Authentication.AllowedHosts.Any(host => string.IsNullOrWhiteSpace(host) || host.Contains('*', StringComparison.Ordinal)) ||
            options.Authentication.AllowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Identidade, recurso HTTPS e hosts permitidos devem ser explícitos. Verifique Authentication:Authority, Authentication:Audience, Authentication:Resource, Authentication:RequireHttpsMetadata, Authentication:AllowedHosts e Authentication:AllowedOrigins.");
        }

        if ((options.Transport.TrustedProxies ?? []).Any(proxy => !System.Net.IPAddress.TryParse(proxy, out _)) ||
            (options.Transport.TrustedNetworks ?? []).Any(network => !System.Net.IPNetwork.TryParse(network, out var parsed) || parsed.PrefixLength == 0))
        {
            errors.Add("Transport:TrustedProxies exige IPs e Transport:TrustedNetworks exige CIDR específicos (nunca 0.0.0.0/0 ou ::/0).");
        }

        if (options.Embeddings.Dimensions is not > 0 || string.IsNullOrWhiteSpace(options.Embeddings.Provider) ||
            string.IsNullOrWhiteSpace(options.Embeddings.Model) || options.Embeddings.SdkAutomaticRetriesEnabled)
        {
            errors.Add("Embedding requer modelo/dimensão e tentativas controladas sem retries ocultos. Verifique Embeddings:Provider, Embeddings:Model (ou OpenAI:EmbeddingModel), Embeddings:Dimensions e Embeddings:SdkAutomaticRetriesEnabled.");
        }

        if (options.Embeddings.Provider == "Fake" && (!allowSynthetic || options.ConfigurationPurpose != "SyntheticTestsOnly_NotApprovedForProduction"))
        {
            errors.Add("Provider Fake é exclusivo dos testes sintéticos.");
        }
        if (options.Processing.EmbeddingStageEnabled == true && options.Embeddings.Provider == "OpenAI" && string.IsNullOrWhiteSpace(options.Embeddings.ApiKey))
        {
            errors.Add("Etapa OpenAI requer chave externa antes de iniciar o worker. Configure OpenAI:ApiKey (ou Embeddings:ApiKey).");
        }

        var openAi = options.OpenAI;
        if ((!string.IsNullOrWhiteSpace(openAi.EmbeddingModel) && !string.IsNullOrWhiteSpace(options.Embeddings.Model) &&
                openAi.EmbeddingModel != options.Embeddings.Model) ||
            (!string.IsNullOrWhiteSpace(openAi.ApiKey) && !string.IsNullOrWhiteSpace(options.Embeddings.ApiKey) &&
                openAi.ApiKey != options.Embeddings.ApiKey))
        {
            errors.Add("OpenAI:EmbeddingModel/OpenAI:ApiKey divergem de Embeddings:Model/Embeddings:ApiKey; manter uma única origem.");
        }

        var pricing = openAi.Pricing;
        decimal?[] prices = [pricing.Embedding.InputPerMillionTokens, pricing.Chat.InputPerMillionTokens,
            pricing.Chat.CachedInputPerMillionTokens, pricing.Chat.OutputPerMillionTokens];
        if (prices.Any(price => price is < 0) || (prices.Any(price => price.HasValue) && string.IsNullOrWhiteSpace(pricing.Currency)))
        {
            errors.Add("Preços OpenAI devem ser não negativos e acompanhados de OpenAI:Pricing:Currency.");
        }

        if (options.Processing.Profiles.Select(profile => Guid.TryParse(profile.Id, out var id) ? id : Guid.Empty).Distinct().Count() != options.Processing.Profiles.Length)
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
            if (profile.EmbeddingProvider == "Fake" && (!allowSynthetic || options.ConfigurationPurpose != "SyntheticTestsOnly_NotApprovedForProduction"))
            {
                errors.Add("Perfil Fake é exclusivo dos testes sintéticos.");
            }
            try
            {
                profile.ToDomain().Validate();
            }
            catch (KnowledgeException)
            {
                errors.Add("Perfil de extração/segmentação inválido ou incompleto.");
            }
        }
        if (options.Processing.LeaseSeconds <= options.Processing.TimeoutSeconds)
        {
            errors.Add("Lease deve exceder o timeout para permitir finalização protegida.");
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

    /// <summary>
    /// Perfil usado pela busca MCP: Retrieval:AdoptedProfileId ou, se omitido, o único perfil configurado.
    /// Precisa usar o mesmo provedor/modelo/dimensão de Embeddings; avaliação e aprovação não são exigidas.
    /// </summary>
    public static PerfilProcessamento OperationalProfile(RagOptions options)
    {
        var profiles = options.Processing.Profiles;
        var configured = string.IsNullOrWhiteSpace(options.Retrieval.AdoptedProfileId)
            ? (profiles.Length == 1 ? profiles[0] : null)
            : Guid.TryParse(options.Retrieval.AdoptedProfileId, out var adoptedId)
                ? profiles.SingleOrDefault(profile => Guid.TryParse(profile.Id, out var id) && id == adoptedId) : null;
        if (configured is null || configured.EmbeddingProvider != options.Embeddings.Provider ||
            configured.EmbeddingModel != options.Embeddings.Model || configured.Dimensions != options.Embeddings.Dimensions)
        {
            throw new KnowledgeException("PROFILE_INCOMPATIBLE");
        }

        var profile = configured.ToDomain();
        profile.Validate();
        return profile;
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
