using System.Text;
using Api.Agente.Analise;
using Api.Agente.Infrastructure.Consumption;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Tests.Fixtures;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Unit;

/// <summary>Coordenação determinística, isolamento de dados e bloqueio das etapas dependentes.</summary>
public sealed class AnalysisPipelineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Parecer nunca transforma cobertura estrutural parcial em conformidade fiscal.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task ValidJourneyPreservesDeterministicResultsAndTracksUsage()
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm(); var knowledge = new FakeKnowledge();
        var identity = AnalysisFixtures.Identity;
        using var stream = File();
        var result = await Service(ledger, provider, knowledge).AnalyzeAsync(stream, identity, Token);
        Assert.Equal("partial", result.Status);
        Assert.Equal(identity.ExecutionId, result.ExecutionId);
        Assert.Equal("partial", result.Validation.State);
        Assert.NotNull(result.Parecer);
        Assert.True(result.HumanReviewRequired);
        Assert.Contains("FISCAL_RULES_NOT_EXECUTED", result.ChecksNotPerformed);
        Assert.All(result.ChecksNotPerformed, pending => Assert.Contains(pending, result.Parecer.Limitations));
        Assert.Equal(result.Evidences.Select(evidence => evidence.ReferenceId), result.Parecer.ReferenceIds);
        Assert.Equal(2, knowledge.Calls);
        Assert.Equal(2, result.KnowledgeCalls.Length);
        Assert.Equal(2, ledger.KnowledgeCalls.Count);
        var attempt = Assert.Single(ledger.Attempts.Values);
        Assert.Equal("succeeded", attempt.State); Assert.Equal(11L, attempt.InputTokens); Assert.Equal(5L, attempt.OutputTokens);
        Assert.Null(attempt.EstimatedCost); Assert.Null(attempt.ProviderReportedCost);
        Assert.Equal("partial", ledger.Executions[identity.ExecutionId].State);
    }

    /// <summary>Falhas MCP e ausência de evidência não executam IA.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("failure", "failed", "KNOWLEDGE_UNAVAILABLE")]
    [InlineData("empty", "partial", "NO_ELIGIBLE_EVIDENCE")]
    [InlineData("invalid-reference", "failed", "KNOWLEDGE_REFERENCE_INVALID")]
    [InlineData("withdrawn", "failed", "KNOWLEDGE_REFERENCE_INVALID")]
    [InlineData("changed-origin", "failed", "KNOWLEDGE_REFERENCE_INVALID")]
    public async Task IneligibleOrInconsistentEvidenceBlocksProvider(string scenario, string state, string code)
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm();
        using var stream = File();
        var result = await Service(ledger, provider, new(scenario)).AnalyzeAsync(stream, AnalysisFixtures.Identity, Token);
        Assert.Equal(state, result.Status); Assert.Equal(code, result.ErrorCode);
        Assert.Null(result.Parecer); Assert.Equal(0, provider.Calls); Assert.Empty(ledger.Attempts);
    }

    /// <summary>Mapeamento ausente e arquivo inválido não chegam ao MCP.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("layout", "unsupported")]
    [InlineData("structure", "failed")]
    public async Task UnsupportedOrInvalidDocumentStopsBeforeKnowledge(string scenario, string state)
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm(); var knowledge = new FakeKnowledge();
        using var stream = File(scenario == "structure" ? AnalysisFixtures.File.Replace("|9999|2|", "|9999|9|", StringComparison.Ordinal) : null);
        var result = await Service(ledger, provider, knowledge, scenario != "layout").AnalyzeAsync(stream, AnalysisFixtures.Identity, Token);
        Assert.Equal(state, result.Status); Assert.Null(result.Parecer);
        Assert.Equal(0, knowledge.Calls); Assert.Equal(0, provider.Calls);
    }

    /// <summary>Auditoria indisponível impede leitura funcional e chamadas dependentes.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task AuditFailureBeforeExecutionBlocksAllDependencies()
    {
        var ledger = new MemoryAnalysisLedger { FailStart = true }; var provider = new FakeParecerLlm(); var knowledge = new FakeKnowledge();
        using var stream = File();
        var exception = await Assert.ThrowsAsync<AnalysisException>(() => Service(ledger, provider, knowledge).AnalyzeAsync(stream, AnalysisFixtures.Identity, Token));
        Assert.Equal("AUDIT_UNAVAILABLE", exception.Code); Assert.Equal(0, knowledge.Calls); Assert.Equal(0, provider.Calls);
    }

    /// <summary>Tentativas inválidas e timeout são auditados individualmente; desconhecido é nulo.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("retry", "partial", 2, "failed")]
    [InlineData("invalid", "failed", 2, "failed")]
    [InlineData("timeout", "failed", 2, "timeout")]
    [InlineData("refusal", "failed", 1, "failed")]
    [InlineData("incomplete", "failed", 1, "failed")]
    public async Task AttemptsAreFiniteAndPreserveEveryKnownUsage(string scenario, string state, int calls, string firstState)
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm(scenario);
        using var stream = File();
        var result = await Service(ledger, provider, new()).AnalyzeAsync(stream, AnalysisFixtures.Identity, Token);
        Assert.Equal(state, result.Status); Assert.Equal(calls, provider.Calls); Assert.Equal(calls, ledger.Attempts.Count);
        Assert.Equal(firstState, ledger.Attempts.Values.First().State);
        Assert.All(ledger.Attempts.Values, attempt =>
        {
            Assert.Equal(scenario == "timeout" ? null : 11L, attempt.InputTokens);
            Assert.Null(attempt.EstimatedCost); Assert.Null(attempt.ProviderReportedCost);
        });
    }

    /// <summary>Uso não persistido mantém intenção pendente e impede retry do provedor.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task RecordingFailureDoesNotRepeatBillableCall()
    {
        var ledger = new MemoryAnalysisLedger { FailFinish = true }; var provider = new FakeParecerLlm("invalid");
        using var stream = File();
        var result = await Service(ledger, provider, new()).AnalyzeAsync(stream, AnalysisFixtures.Identity, Token);
        Assert.Equal("failed", result.Status); Assert.Equal("AUDIT_UNAVAILABLE", result.ErrorCode);
        Assert.Null(result.Parecer); Assert.Equal(1, provider.Calls);
        Assert.Equal("pending", Assert.Single(ledger.Attempts.Values).State);
    }

    /// <summary>Cancelamento do solicitante registra tentativa/execução canceladas com token independente.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task CallerCancellationStillRecordsAttempt()
    {
        var ledger = new MemoryAnalysisLedger(); var provider = new FakeParecerLlm("timeout");
        var identity = AnalysisFixtures.Identity;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        using var stream = File();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(ledger, provider, new()).AnalyzeAsync(stream, identity, cancellation.Token));
        Assert.Equal("cancelled", Assert.Single(ledger.Attempts.Values).State);
        Assert.Equal("cancelled", ledger.Executions[identity.ExecutionId].State);
        Assert.Equal(1, provider.Calls);
    }

    private static MemoryStream File(string? contents = null) => new(Encoding.Latin1.GetBytes(contents ?? AnalysisFixtures.File));
    private static AnaliseSpedService Service(MemoryAnalysisLedger ledger, FakeParecerLlm provider, FakeKnowledge knowledge, bool mapped = true) =>
        new(SpedParserTests.Create(), new SpedValidator(Options.Create(mapped ? AnalysisFixtures.Layouts : new LayoutPolicyOptions())), knowledge,
            new MeasuredParecerLlm(provider, ledger, Options.Create(AnalysisFixtures.Llm)), ledger, Options.Create(new AgentDataOptions { TimeoutSeconds = 5 }));
}
