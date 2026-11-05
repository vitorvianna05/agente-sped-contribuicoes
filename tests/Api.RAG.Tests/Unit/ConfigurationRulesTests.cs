using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;

namespace Api.RAG.Tests.Unit;

/// <summary>Invariantes de configuração T007, independentes de serviços externos.</summary>
public sealed class ConfigurationRulesTests
{
    /// <summary>Uma política incompleta nunca autoriza expurgo.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingRetentionPreventsExpurge()
    {
        Assert.Equal("RETENTION_NOT_CONFIGURED", Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequireExpurge(new RagOptions())).Code);
    }

    /// <summary>Ausência de perfil é falha explícita de configuração.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingProfileIsConfigurationFailure()
    {
        var error = Assert.Throws<KnowledgeException>(() => RagOptionsValidator.RequireOperationalProfile(new RagOptions()));
        Assert.Contains("PROFILE", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Startup não aceita limites ausentes.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MissingLimitsFailValidation()
    {
        Assert.True(new RagOptionsValidator().Validate(null, new RagOptions()).Failed);
    }
}
