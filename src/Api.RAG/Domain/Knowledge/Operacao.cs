using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Domain.Knowledge;

internal sealed record Operacao
{
    public required Guid OperationId { get; init; }
    public required string Type { get; init; }
    public Guid? DocumentVersionId { get; init; }
    public Guid? GenerationId { get; init; }
    public required string PrincipalId { get; init; }
    public required string CorrelationId { get; init; }
    public required string State { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public DateTimeOffset? LeaseUntil { get; init; }
    public string? LeaseOwner { get; init; }
    public int AttemptNumber { get; init; }
    public string? ResultCode { get; init; }
    public required ContentHash IdempotencyKeyDigest { get; init; }
    public required ContentHash RequestDigest { get; init; }

    public void Validate()
    {
        if (OperationId == Guid.Empty || string.IsNullOrWhiteSpace(PrincipalId) || string.IsNullOrWhiteSpace(CorrelationId) ||
            Type is not ("ingestion" or "approve" or "reprocess" or "withdraw" or "expurge") ||
            State is not ("queued" or "processing" or "succeeded" or "failed" or "cancelled") ||
            CreatedAt.Offset != TimeSpan.Zero || AttemptNumber < 0 ||
            new[] { StartedAt, FinishedAt, LeaseUntil }.Any(date => date is { Offset: var offset } && offset != TimeSpan.Zero) ||
            ((LeaseUntil is null) != (LeaseOwner is null)) ||
            (State == "processing" && (StartedAt is null || LeaseUntil is null || string.IsNullOrWhiteSpace(LeaseOwner))) ||
            (State is "succeeded" or "failed" or "cancelled" && FinishedAt is null) ||
            (FinishedAt < CreatedAt) || (FinishedAt < StartedAt) || (StartedAt < CreatedAt))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
    }
}
