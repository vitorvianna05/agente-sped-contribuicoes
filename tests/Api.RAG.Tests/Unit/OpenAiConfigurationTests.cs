using Api.RAG.Infrastructure.Configuration;

namespace Api.RAG.Tests.Unit;

/// <summary>Seção OpenAI: origem de chave/modelo, ativação da etapa vetorial e preços.</summary>
public sealed class OpenAiConfigurationTests
{
    private static RagOptions WithOpenAi(string? key = "synthetic-key-test-only")
    {
        var options = new RagOptions();
        options.Embeddings.Provider = "OpenAI";
        options.OpenAI.ApiKey = key;
        options.OpenAI.EmbeddingModel = "text-embedding-3-small";
        return options;
    }

    /// <summary>Chave e modelo da seção OpenAI alimentam Embeddings quando este não os define.</summary>
    [Fact, Trait("Category", "Unit")]
    public void OpenAiSectionFillsEmbeddingKeyAndModel()
    {
        var options = WithOpenAi();
        options.ApplyOpenAiSection();
        Assert.Equal("synthetic-key-test-only", options.Embeddings.ApiKey);
        Assert.Equal("text-embedding-3-small", options.Embeddings.Model);
    }

    /// <summary>Sem flag explícita, a etapa vetorial acompanha a presença da chave.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("synthetic-key-test-only", null, true)]
    [InlineData("", null, false)]
    [InlineData("synthetic-key-test-only", false, false)]
    public void EmbeddingStageFollowsKeyUnlessExplicit(string key, bool? flag, bool expected)
    {
        var options = WithOpenAi(key);
        options.Processing.EmbeddingStageEnabled = flag;
        options.ApplyOpenAiSection();
        Assert.Equal(expected, options.EmbeddingStageActive);
    }

    /// <summary>Ativação explícita sem chave falha no startup, sem expor segredo.</summary>
    [Fact, Trait("Category", "Unit")]
    public void ExplicitStageWithoutKeyFails()
    {
        var options = WithOpenAi("");
        options.Processing.EmbeddingStageEnabled = true;
        options.ApplyOpenAiSection();
        var message = string.Join("; ", new RagOptionsValidator().Validate(null, options).Failures!);
        Assert.Contains("OpenAI:ApiKey", message, StringComparison.Ordinal);
    }

    /// <summary>Modelos divergentes entre as seções são rejeitados.</summary>
    [Fact, Trait("Category", "Unit")]
    public void DivergentModelsFail()
    {
        var options = WithOpenAi();
        options.Embeddings.Model = "text-embedding-3-large";
        options.ApplyOpenAiSection();
        var message = string.Join("; ", new RagOptionsValidator().Validate(null, options).Failures!);
        Assert.Contains("OpenAI:EmbeddingModel", message, StringComparison.Ordinal);
    }

    /// <summary>Preço de embedding vira tabela versionada; preço sem moeda ou negativo é rejeitado.</summary>
    [Fact, Trait("Category", "Unit")]
    public void EmbeddingPricingBecomesConsumptionPrice()
    {
        var options = WithOpenAi();
        options.OpenAI.Pricing.Currency = "USD";
        options.OpenAI.Pricing.Embedding.InputPerMillionTokens = 0.02m;
        var price = options.OpenAI.Pricing.ToEmbeddingPrice()!;
        Assert.Equal(0.02m, price.PerMillionEmbeddingTokens);
        Assert.Equal("USD", price.Currency);
        Assert.StartsWith("cfg-", price.Version, StringComparison.Ordinal);

        options.OpenAI.Pricing.Currency = null;
        Assert.Null(options.OpenAI.Pricing.ToEmbeddingPrice());
        var message = string.Join("; ", new RagOptionsValidator().Validate(null, options).Failures!);
        Assert.Contains("OpenAI:Pricing:Currency", message, StringComparison.Ordinal);
    }
}
