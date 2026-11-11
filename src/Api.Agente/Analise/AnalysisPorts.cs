using Api.Agente.Contracts;
using Api.Agente.Domain;
using Api.Agente.Llm;
using Api.Agente.Mcp.Contracts;

namespace Api.Agente.Analise;

internal sealed record ExecutionIdentity(Guid ExecutionId, string CorrelationId, string PrincipalId, string? OrganizationId);
internal sealed record ParecerContext(ValidationResult Validation, KnowledgeEvidence[] Evidences);
internal sealed record LlmResponse(string? Json, long? InputTokens, long? OutputTokens, string State, long? CachedInputTokens = null);
internal sealed record ConsumptionAttempt(Guid AttemptId, string State, string Model, string PromptVersion,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, long? InputTokens, long? OutputTokens,
    decimal? EstimatedCost, string? EstimatedCurrency, decimal? ProviderReportedCost, string? Currency,
    string? PriceReference, string? PriceVersion, long? CachedInputTokens = null)
{
    public double? DurationMilliseconds => FinishedAt.HasValue ? (FinishedAt.Value - StartedAt).TotalMilliseconds : null;
}

internal interface IParecerLlm
{
    Task<LlmResponse> GenerateAsync(ParecerContext context, CancellationToken cancellationToken);
}

internal interface IAnalysisLedger
{
    Task StartAsync(ExecutionIdentity identity, CancellationToken token);
    Task RecordKnowledgeAsync(ExecutionIdentity identity, KnowledgeTrace trace, CancellationToken token);
    Task<Guid> BeginAttemptAsync(ExecutionIdentity identity, string model, string promptVersion, CancellationToken token);
    Task FinishAttemptAsync(Guid attemptId, string state, LlmResponse? usage, LlmCost? cost, CancellationToken token);
    Task CompleteAsync(ExecutionIdentity identity, string state, string? code, CancellationToken token);
    Task<ConsumptionAttempt[]> ReadAsync(Guid executionId, string principal, string? organization, CancellationToken token);
}

internal sealed class AnalysisException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
