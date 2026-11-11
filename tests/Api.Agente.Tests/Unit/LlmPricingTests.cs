using Api.Agente.Analise;
using Api.Agente.Infrastructure;
using Api.Agente.Infrastructure.Consumption;
using Api.Agente.Llm;
using Api.Agente.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Unit;

/// <summary>Custo estimado do parecer: tabela configurada, tokens conhecidos e nunca zero presumido.</summary>
public sealed class LlmPricingTests
{
    private static LlmPricingOptions Table => new()
    {
        Currency = "USD",
        Reference = "synthetic-table",
        InputPerMillionTokens = 0.4m,
        CachedInputPerMillionTokens = 0.1m,
        OutputPerMillionTokens = 1.6m
    };

    /// <summary>Input em cache usa o preço próprio; o restante do input e o output usam os seus.</summary>
    [Fact, Trait("Category", "Unit")]
    public void EstimateSeparatesCachedInput()
    {
        var cost = Table.ToPrice()!.Estimate(new LlmResponse(null, 1_000_000, 500_000, "succeeded", 400_000));
        Assert.Equal(1.08m, cost!.Amount);
        Assert.Equal(("USD", "synthetic-table"), (cost.Currency, cost.Reference));
        Assert.StartsWith("cfg-", cost.Version, StringComparison.Ordinal);
    }

    /// <summary>Cache não informado conta como input integral; sem preço de cache usa o do input.</summary>
    [Fact, Trait("Category", "Unit")]
    public void MissingCacheDataFallsBackToInputPrice()
    {
        Assert.Equal(1.2m, Table.ToPrice()!.Estimate(new LlmResponse(null, 1_000_000, 500_000, "succeeded"))!.Amount);
        var noCachePrice = Table; noCachePrice.CachedInputPerMillionTokens = null;
        Assert.Equal(1.2m, noCachePrice.ToPrice()!.Estimate(new LlmResponse(null, 1_000_000, 500_000, "succeeded", 400_000))!.Amount);
    }

    /// <summary>Tokens desconhecidos ou incoerentes → custo desconhecido, não zero.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData(null, 5L, null)]
    [InlineData(10L, null, null)]
    [InlineData(10L, 5L, 11L)]
    public void UnknownOrInconsistentUsageHasNoCost(long? input, long? output, long? cached)
    {
        Assert.Null(Table.ToPrice()!.Estimate(new LlmResponse(null, input, output, "failed", cached)));
        Assert.Null(Table.ToPrice()!.Estimate(null));
    }

    /// <summary>Versão explícita é preservada; derivada muda quando os valores mudam.</summary>
    [Fact, Trait("Category", "Unit")]
    public void VersionIsExplicitOrDerivedFromValues()
    {
        var explicitVersion = Table; explicitVersion.Version = "2026-11";
        Assert.Equal("2026-11", explicitVersion.ToPrice()!.Version);
        var changed = Table; changed.OutputPerMillionTokens = 2m;
        Assert.Equal(Table.ToPrice()!.Version, Table.ToPrice()!.Version);
        Assert.NotEqual(Table.ToPrice()!.Version, changed.ToPrice()!.Version);
        Assert.Equal("configuration:Llm:Pricing", new LlmPricingOptions { Currency = "USD", InputPerMillionTokens = 1, OutputPerMillionTokens = 1 }.ToPrice()!.Reference);
    }

    /// <summary>Sem preços: válido e custo desconhecido. Preço parcial, negativo ou sem moeda falha no startup.</summary>
    [Fact, Trait("Category", "Unit")]
    public void ValidatorRejectsIncompleteOrNegativePrices()
    {
        var validator = new AnalysisOptionsValidator();
        Assert.True(validator.Validate(null, AnalysisFixtures.Llm).Succeeded);
        Assert.Null(AnalysisFixtures.Llm.Pricing.ToPrice());
        var withTable = AnalysisFixtures.Llm; withTable.Pricing = Table;
        Assert.True(validator.Validate(null, withTable).Succeeded);
        foreach (var broken in new Action<LlmPricingOptions>[]
        {
            table => table.Currency = null, table => table.OutputPerMillionTokens = null,
            table => table.InputPerMillionTokens = -1, table => table.CachedInputPerMillionTokens = -0.1m
        })
        {
            var options = AnalysisFixtures.Llm; options.Pricing = Table; broken(options.Pricing);
            Assert.True(validator.Validate(null, options).Failed);
        }
    }

    /// <summary>Cada tentativa medida registra o custo com a tabela configurada.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task MeasuredAttemptRecordsEstimatedCost()
    {
        var ledger = new MemoryAnalysisLedger();
        var options = AnalysisFixtures.Llm; options.Pricing = Table;
        await new MeasuredParecerLlm(new FakeParecerLlm(), ledger, Options.Create(options))
            .GenerateAsync(AnalysisFixtures.Identity, ParecerValidationTests.Context, TestContext.Current.CancellationToken);
        var attempt = Assert.Single(ledger.Attempts.Values);
        Assert.Equal((11 * 0.4m + 5 * 1.6m) / 1_000_000m, attempt.EstimatedCost);
        Assert.Equal(("USD", "synthetic-table"), (attempt.EstimatedCurrency, attempt.PriceReference));
        Assert.Null(attempt.ProviderReportedCost);
    }
}
