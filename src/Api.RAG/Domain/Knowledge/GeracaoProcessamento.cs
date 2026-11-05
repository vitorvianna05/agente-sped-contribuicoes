using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Domain.Knowledge;

internal sealed record GeracaoProcessamento(Guid GenerationId, Guid DocumentVersionId, Guid OperationId,
    Guid ProfileId, ContentHash SourceHash, string State, DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt = null, DateTimeOffset? SupersededAt = null,
    ContentHash? ContentManifestHash = null, string? FailureCode = null,
    string PreparationState = "queued", Guid? PreparationLeaseOwner = null,
    DateTimeOffset? PreparationLeaseUntil = null, int PreparationAttempts = 0)
{
    public void Validate()
    {
        if (GenerationId == Guid.Empty || DocumentVersionId == Guid.Empty || OperationId == Guid.Empty || ProfileId == Guid.Empty ||
            State is not ("queued" or "processing" or "succeeded" or "failed" or "cancelled") || CreatedAt.Offset != TimeSpan.Zero ||
            new[] { CompletedAt, SupersededAt }.Any(date => date is { Offset: var offset } && offset != TimeSpan.Zero) ||
            CompletedAt < CreatedAt || SupersededAt < CompletedAt ||
            (State is "succeeded" or "failed" or "cancelled" && CompletedAt is null) ||
            (State == "succeeded" && ContentManifestHash is null))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
    }

    public GeracaoProcessamento Start() => State == "queued" ? this with { State = "processing" }
        : throw new KnowledgeException("INVALID_STATE");

    public GeracaoProcessamento Finish(string state, DateTimeOffset at, ContentHash? manifest, string? failure)
    {
        if (State != "processing" || state is not ("succeeded" or "failed" or "cancelled") ||
            at.Offset != TimeSpan.Zero || at < CreatedAt || (state == "succeeded" && manifest is null))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        return this with { State = state, CompletedAt = at, ContentManifestHash = manifest, FailureCode = failure };
    }

    public GeracaoProcessamento ClaimPreparation(Guid owner, DateTimeOffset now, TimeSpan lease, int maximumAttempts)
    {
        Validate();
        if (owner == Guid.Empty || now.Offset != TimeSpan.Zero || now < CreatedAt || lease <= TimeSpan.Zero || maximumAttempts <= 0 ||
            PreparationState is not ("queued" or "processing") ||
            (PreparationState == "processing" && (PreparationLeaseUntil is null || PreparationLeaseUntil > now)))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        var processing = State == "queued" ? Start() : State == "processing" ? this : throw new KnowledgeException("INVALID_STATE");
        if (PreparationAttempts >= maximumAttempts)
        {
            return processing.Finish("failed", now, null, "PROCESSING_ATTEMPTS_EXHAUSTED") with
            {
                PreparationState = "failed",
                PreparationLeaseOwner = null,
                PreparationLeaseUntil = null
            };
        }
        return processing with
        {
            PreparationState = "processing",
            PreparationLeaseOwner = owner,
            PreparationLeaseUntil = now + lease,
            PreparationAttempts = PreparationAttempts + 1
        };
    }

    public GeracaoProcessamento CompletePreparation(Guid owner, DateTimeOffset at, ContentHash? textManifest, string? failure)
    {
        if (PreparationState != "processing" || PreparationLeaseOwner != owner || PreparationLeaseUntil is null || PreparationLeaseUntil <= at)
        {
            throw new KnowledgeException("REVISION_MISMATCH");
        }
        if (State != "processing" || at.Offset != TimeSpan.Zero || at < CreatedAt || (failure is null && textManifest is null))
        {
            throw new KnowledgeException("INVALID_STATE");
        }
        var result = failure is null ? this : Finish("failed", at, null, failure);
        return result with
        {
            PreparationState = failure is null ? "ready" : "failed",
            PreparationLeaseOwner = null,
            PreparationLeaseUntil = null
        };
    }
}
