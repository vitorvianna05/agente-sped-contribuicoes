using System.Text.Json;
using Api.Agente.Contracts;

namespace Api.Agente.Tests.Contracts;

/// <summary>Contratos não permitem que a entrada ou o LLM atribuam resultados operacionais.</summary>
public sealed class AnalysisContractTests
{
    /// <summary>Todos os estados técnicos são distintos de uma decisão tributária.</summary>
    [Theory, Trait("Category", "Contract")]
    [InlineData("completed")]
    [InlineData("partial")]
    [InlineData("failed")]
    [InlineData("unsupported")]
    public void TechnicalStatesAreExplicit(string status)
    {
        var result = new AnalysisResult("1.0", Guid.NewGuid(), "system-correlation", status,
            new(status, null, null, null, null, null, [], []), [], null,
            ["Synthetic technical limitation"], [], null, true, []);
        result.Validate();
        Assert.Throws<JsonException>(() => (result with { Status = "compliant" }).Validate());
        Assert.Throws<JsonException>(() => (result with { Status = "completed", ChecksNotPerformed = ["mandatory"] }).Validate());
    }

    /// <summary>JSON do modelo não pode atribuir metadados de execução do sistema.</summary>
    [Fact, Trait("Category", "Contract")]
    public void LlmContractIsSeparateFromEnvelope()
    {
        var draft = new ParecerDraft("Synthetic explanation", [], [], ["Not assessed"], true);
        var json = JsonSerializer.Serialize(draft, AnalysisJson.Options);
        Assert.DoesNotContain("executionId", json, StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ParecerDraft>(
            json.TrimEnd('}') + ",\"executionId\":\"invented\"}", AnalysisJson.Options));
    }
}
