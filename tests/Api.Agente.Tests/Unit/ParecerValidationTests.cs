using System.Text.Json.Nodes;
using Api.Agente.Analise;
using Api.Agente.Contracts;
using Api.Agente.Tests.Fixtures;

namespace Api.Agente.Tests.Unit;

/// <summary>JSON do modelo não altera validações nem cria citações ou metadados operacionais.</summary>
public sealed class ParecerValidationTests
{
    internal static ParecerContext Context => new(new("partial", "006", new(2020, 1, 1), new(2020, 1, 31), "synthetic-type", "synthetic-layout",
        [], ["FISCAL_RULES_NOT_EXECUTED"]), [AnalysisFixtures.Evidence]);

    /// <summary>Referências e limitações presentes no contexto são aceitas com revisão humana.</summary>
    [Fact, Trait("Category", "Unit")]
    public void ValidDraftPreservesSystemContext()
    {
        var context = Context;
        var draft = ParecerValidator.Read(AnalysisFixtures.Draft(context), context, 10000);
        Assert.Equal(context.Evidences[0].ReferenceId, Assert.Single(draft.ReferenceIds));
        Assert.True(draft.HumanReviewRequired);
    }

    /// <summary>Cada violação impede publicar o parecer como válido.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("unknown-reference")]
    [InlineData("missing-limitation")]
    [InlineData("invented-check")]
    [InlineData("no-review")]
    [InlineData("execution")]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("null-array")]
    [InlineData("null-item")]
    public void InvalidDraftCannotOverrideValidation(string mutation)
    {
        var context = Context;
        var node = JsonNode.Parse(AnalysisFixtures.Draft(context))!;
        switch (mutation)
        {
            case "unknown-reference": node["referenceIds"] = new JsonArray(Guid.NewGuid().ToString()); break;
            case "missing-limitation": node["limitations"] = new JsonArray(); break;
            case "invented-check": node["interpretedInconsistencies"] = new JsonArray("INVENTED_RULE"); break;
            case "no-review": node["humanReviewRequired"] = false; break;
            case "execution": node["executionId"] = Guid.NewGuid().ToString(); break;
            case "null-array": node["referenceIds"] = null; break;
            case "null-item": node["limitations"] = new JsonArray("FISCAL_RULES_NOT_EXECUTED", (string?)null); break;
        }
        var json = mutation == "malformed" ? "not-json" : node.ToJsonString();
        if (mutation == "duplicate") { json = json[..^1] + ",\"explanation\":\"override\"}"; }
        Assert.Equal("PARECER_INVALID", Assert.Throws<AnalysisException>(() => ParecerValidator.Read(json, context, 10000)).Code);
    }
}
