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

    public Operacao StartPreparation(Guid owner, DateTimeOffset at, TimeSpan lease)
    {
        Validate();
        if (State is not ("queued" or "processing") || owner == Guid.Empty || lease <= TimeSpan.Zero ||
            (State == "processing" && LeaseUntil > at))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        var result = this with
        {
            State = "processing",
            StartedAt = StartedAt ?? at,
            LeaseUntil = at + lease,
            LeaseOwner = owner.ToString(),
            AttemptNumber = AttemptNumber + 1,
            ResultCode = "TEXT_PREPARING"
        };
        result.Validate();
        return result;
    }

    public Operacao CompletePreparation(string? failure, DateTimeOffset at)
    {
        Validate();
        if (State != "processing")
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        var result = this with
        {
            State = failure is null ? "queued" : "failed",
            LeaseUntil = null,
            LeaseOwner = null,
            ResultCode = failure ?? "EMBEDDINGS_PENDING",
            FinishedAt = failure is null ? null : at
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (OperationId == Guid.Empty || string.IsNullOrWhiteSpace(PrincipalId) || string.IsNullOrWhiteSpace(CorrelationId) ||
            Type is not ("ingestion" or "approve" or "reprocess" or "withdraw" or "expurge" or "query") ||
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

    public Operacao FinishWork(string? failure, DateTimeOffset at, bool cancelled = false)
    {
        if (State != "processing")
        {
            throw new KnowledgeException("INVALID_STATE");
        }

        var result = this with { State = cancelled ? "cancelled" : failure is null ? "succeeded" : "failed", FinishedAt = at, LeaseUntil = null, LeaseOwner = null, ResultCode = failure };
        result.Validate();
        return result;
    }
}
