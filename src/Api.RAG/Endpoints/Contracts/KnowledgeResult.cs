namespace Api.RAG.Endpoints.Contracts;

internal sealed record KnowledgeResult(
    string ContractVersion, string CorrelationId, string Status,
    KnowledgeEvidence[] Evidences, KnowledgeLimitation[] Limitations, KnowledgeError? Error);

internal sealed record KnowledgeEvidence(
    Guid ReferenceId, Guid SourceId, Guid DocumentVersionId, Guid GenerationId, Guid ChunkId,
    string Issuer, string OfficialIdentifier, string OfficialVersion, Uri OriginUri, string Text,
    KnowledgeApplicability Applicability, KnowledgeLocation Location, string ReferenceState, double? Score);

internal sealed record KnowledgeApplicability(string DocumentType, string LayoutVersion, string OfficialBasis);
internal sealed record KnowledgeLocation(string Availability, int[] PhysicalPages, string[] PrintedLabels, string? Section, string? Reason);
internal sealed record KnowledgeLimitation(string Code, string Message);
internal sealed record KnowledgeError(string Code, string Message, bool Retryable);
