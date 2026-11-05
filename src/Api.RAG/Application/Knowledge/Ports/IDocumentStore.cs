using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record IngestionSubmission(IngestionMetadata Metadata, PerfilProcessamento Profile, byte[] Original,
    Operacao Operation, string? OrganizationId, string RetentionPolicyVersion);
internal sealed record PreparationJob(Guid GenerationId, Guid OperationId, byte[] Original, PerfilProcessamento Profile,
    Guid LeaseOwner, string PrincipalId, string CorrelationId);
internal sealed record OperationSnapshot(Guid OperationId, string Type, string State, Guid? DocumentVersionId,
    Guid? GenerationId, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    string? ResultCode, string? PreparationState, string Revision);
internal sealed record DocumentSnapshot(Guid DocumentVersionId, IngestionMetadata Metadata, string OriginalContentHash,
    string PublicationState, Guid? ActiveGenerationId, string Revision, DateTimeOffset CreatedAt);

internal interface IDocumentStore
{
    Task<OperationSnapshot> SubmitAsync(IngestionSubmission submission, CancellationToken cancellationToken);
    Task<OperationSnapshot> GetOperationAsync(Guid id, CancellationToken cancellationToken);
    Task<DocumentSnapshot> GetDocumentAsync(Guid id, CancellationToken cancellationToken);
    Task<PreparationJob?> ClaimAsync(DateTimeOffset now, TimeSpan lease, int maximumAttempts, CancellationToken cancellationToken);
    Task FinishPreparationAsync(PreparationJob job, IReadOnlyList<Trecho> chunks, string? failureCode,
        DateTimeOffset at, CancellationToken cancellationToken);
}
