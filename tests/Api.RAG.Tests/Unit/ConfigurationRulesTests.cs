using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;

namespace Api.RAG.Tests.Unit;

/// <summary>Invariantes de configuração T007, independentes de serviços externos.</summary>
public sealed class ConfigurationRulesTests
{
    /// <summary>Uma política incompleta nunca autoriza persistência.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingRetentionPreventsPersistence()
    {
        var options = new RagOptions();
        options.ConnectionStrings.Rag = "Host=localhost;Database=synthetic;Username=app";
        Assert.Equal("RETENTION_NOT_CONFIGURED", Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequirePersistence(options, false)).Code);
    }

    /// <summary>Ausência de perfil, ou mais de um sem Retrieval:AdoptedProfileId, é falha explícita de configuração.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingProfileIsConfigurationFailure()
    {
        var error = Assert.Throws<KnowledgeException>(() => RagOptionsValidator.OperationalProfile(new RagOptions()));
        Assert.Contains("PROFILE", error.Message, StringComparison.Ordinal);
        var ambiguous = new RagOptions();
        ambiguous.Processing.Profiles = [new ProcessingProfile { Id = Guid.NewGuid().ToString() }, new ProcessingProfile { Id = Guid.NewGuid().ToString() }];
        Assert.Equal("PROFILE_INCOMPATIBLE", Assert.Throws<KnowledgeException>(() => RagOptionsValidator.OperationalProfile(ambiguous)).Code);
    }

    /// <summary>Startup não aceita limites ausentes.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingLimitsFailValidation()
    {
        Assert.True(new RagOptionsValidator().Validate(null, new RagOptions()).Failed);
    }

    /// <summary>Configuração vazia identifica as chaves necessárias sem incluir valores secretos.</summary>
    [Fact, Trait("Category", "Unit")]
    public void MissingConfigurationNamesRequiredKeys()
    {
        var options = new RagOptions();
        options.Embeddings.ApiKey = "synthetic-secret-must-not-appear";
        var result = new RagOptionsValidator().Validate(null, options);
        var message = string.Join("; ", result.Failures!);
        Assert.Contains("Processing:MaxFileBytes", message, StringComparison.Ordinal);
        Assert.Contains("Processing:MaxPreparationAttempts", message, StringComparison.Ordinal);
        Assert.Contains("Authentication:Authority", message, StringComparison.Ordinal);
        Assert.Contains("Embeddings:Model", message, StringComparison.Ordinal);
        Assert.DoesNotContain(options.Embeddings.ApiKey, message, StringComparison.Ordinal);
    }
}
