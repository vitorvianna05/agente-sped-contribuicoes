using System.Text;
using Api.Agente.Domain;
using Api.Agente.Infrastructure;
using Api.Agente.Tests.Fixtures;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Unit;

/// <summary>Período e mapeamento aprovados são separados do COD_VER declarado.</summary>
public sealed class SpedValidationTests
{
    /// <summary>Mesmo arquivo pode ser legível sem mapeamento de conhecimento autorizado.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task LayoutMappingIsExplicitAndDoesNotApproveFiscalCoverage()
    {
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(AnalysisFixtures.File));
        var document = await SpedParserTests.Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal("unsupported", new SpedValidator(Options.Create(new LayoutPolicyOptions())).Validate(document).State);
        var validation = new SpedValidator(Options.Create(AnalysisFixtures.Layouts)).Validate(document);
        Assert.Equal("partial", validation.State);
        Assert.Equal("synthetic-layout", validation.KnowledgeLayoutVersion);
        Assert.Equal(new DateOnly(2020, 1, 1), validation.PeriodFrom);
        Assert.Contains("FISCAL_RULES_NOT_EXECUTED", validation.ChecksNotPerformed);
    }

    /// <summary>Datas incorretas e períodos fora do escopo não alimentam consultas presumidas.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("01012027", "31012027", "unsupported")]
    [InlineData("01012019", "31012019", "unsupported")]
    [InlineData("32012020", "31012020", "failed")]
    [InlineData("31012020", "01012020", "failed")]
    public async Task UnsupportedOrInvalidPeriodIsExplicit(string start, string end, string state)
    {
        var file = $"|0000|006|0|||{start}|{end}|Synthetic|\r\n|9999|2|\r\n";
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes(file));
        var document = await SpedParserTests.Create().ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(state, SpedFileValidation.Validate(document, AnalysisFixtures.Layouts.Profiles.Select(profile => profile.ToDomain()).ToArray()).State);
    }

    /// <summary>Perfis sem fonte/aprovação ou com períodos sobrepostos são rejeitados.</summary>
    [Fact, Trait("Category", "Unit")]
    public void UnapprovedAndAmbiguousMappingsCannotStart()
    {
        var options = AnalysisFixtures.Layouts;
        var validator = new AnalysisOptionsValidator();
        Assert.True(validator.Validate(null, options).Succeeded);
        options.Profiles[0].ApprovalReference = "";
        Assert.True(validator.Validate(null, options).Failed);
        options = AnalysisFixtures.Layouts;
        options.Profiles = [options.Profiles[0], options.Profiles[0]];
        Assert.True(validator.Validate(null, options).Failed);
        options = AnalysisFixtures.Layouts;
        options.Profiles = [options.Profiles[0], AnalysisFixtures.Layouts.Profiles[0]];
        Assert.True(validator.Validate(null, options).Failed);
    }
}
