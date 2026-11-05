using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Tests.Unit;

/// <summary>Regras monetárias puras, independentes do repositório e do provedor.</summary>
public sealed class ConsumptionMeasurementTests
{
    /// <summary>Moeda da tabela não preenche moeda omitida pelo provedor.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void EstimateDoesNotInventProviderCurrency()
    {
        var result = ConsumptionMeasurement.Create(null, null, 100, 0.5m, null,
            new ConsumptionPrice(2m, "TEST-ESTIMATE", "synthetic-table", "v1"));
        Assert.Equal(0.0002m, result.EstimatedCost);
        Assert.Equal("TEST-ESTIMATE", result.EstimatedCurrency);
        Assert.Equal(0.5m, result.ProviderReportedCost);
        Assert.Null(result.Currency);
    }

    /// <summary>Valores em moedas diferentes são preservados, sem conversão presumida.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void DifferentCurrenciesRemainIndependent()
    {
        var result = ConsumptionMeasurement.Create(null, null, 100, 0.5m, "TEST-REPORTED",
            new ConsumptionPrice(2m, "TEST-ESTIMATE", "synthetic-table", "v1"));
        Assert.Equal("TEST-REPORTED", result.Currency);
        Assert.Equal("TEST-ESTIMATE", result.EstimatedCurrency);
        Assert.Equal(0.5m, result.ProviderReportedCost);
        Assert.Equal(0.0002m, result.EstimatedCost);
    }

    /// <summary>Ausência de tokens ou preço mantém a estimativa desconhecida.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void UnknownMetricsAndPricesRemainNull()
    {
        var unknown = ConsumptionMeasurement.Create(null, null, null, null, null, null);
        Assert.Null(unknown.EstimatedCost);
        Assert.Null(unknown.EstimatedCurrency);
        Assert.Null(unknown.ProviderReportedCost);
        Assert.Null(unknown.Currency);
        Assert.Equal("unavailable", unknown.UsageAvailability);
        var noTokens = ConsumptionMeasurement.Create(null, null, null, null, null, new ConsumptionPrice(2m, "TEST", "table", "v1"));
        Assert.Null(noTokens.EstimatedCost);
        Assert.Null(ConsumptionMeasurement.Create(null, null, 100, null, null, null).EstimatedCost);
        Assert.Equal(0m, ConsumptionMeasurement.Create(null, null, 0, null, null, new ConsumptionPrice(2m, "TEST", "table", "v1")).EstimatedCost);
    }

    /// <summary>Dados negativos e preço sem referência são rejeitados no domínio.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void InvalidMeasurementCannotReachPersistence()
    {
        Assert.Throws<KnowledgeException>(() => ConsumptionMeasurement.Create(null, null, -1, null, null, null));
        Assert.Throws<KnowledgeException>(() => ConsumptionMeasurement.Create(null, null, null, -1m, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsumptionPrice(-1m, "TEST", "table", "v1"));
        Assert.Throws<ArgumentException>(() => new ConsumptionPrice(1m, "TEST", "", "v1"));
    }
}
