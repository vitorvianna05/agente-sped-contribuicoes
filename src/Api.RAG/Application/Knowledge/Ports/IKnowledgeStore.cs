using System.Security.Claims;
using Api.RAG.Domain.Knowledge;

namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record VectorChunk(Guid ChunkId, string Text, float[]? Vector);
internal sealed record EmbeddingJob(Guid GenerationId, Guid OperationId, Guid Owner, PerfilProcessamento Profile, VectorChunk[] Chunks, string CorrelationId);
internal sealed record RetrievedEvidence(Guid ReferenceId, Guid SourceId, Guid DocumentVersionId, Guid GenerationId, Guid ChunkId,
    string Issuer, string OfficialIdentifier, string OfficialVersion, Uri OriginUri, string Text, string DocumentType,
    string LayoutVersion, string OfficialBasis, ValuesLocation Location, string ReferenceState, double? Score);
internal sealed record ValuesLocation(string Availability, int[] PhysicalPages, string[] PrintedLabels, string? Section, string? Reason);
internal sealed record EvidenceOutcome(string Status, RetrievedEvidence[] Evidences, string LimitationCode, string? ErrorCode = null);

internal interface IKnowledgeStore
{
    Task<EmbeddingJob?> ClaimEmbeddingAsync(DateTimeOffset now, TimeSpan lease, int maximumAttempts, CancellationToken cancellationToken);
    Task SaveVectorAsync(EmbeddingJob job, Guid chunkId, float[] vector, DateTimeOffset at, CancellationToken cancellationToken);
    Task CompleteEmbeddingAsync(EmbeddingJob job, string? failure, DateTimeOffset at, CancellationToken cancellationToken);
    Task<RetrievedEvidence[]> SearchAsync(float[] vector, PerfilProcessamento profile, string type, string layout, CancellationToken cancellationToken);
    Task<(RetrievedEvidence? Evidence, bool Purged)> ResolveAsync(Guid referenceId, CancellationToken cancellationToken);
    Task<Guid> BeginQueryAsync(string principal, string? organization, string correlation, DateTimeOffset at, CancellationToken cancellationToken);
    Task AuditQueryAsync(Guid queryId, string principal, string? organization, string correlation, string result, DateTimeOffset at, CancellationToken cancellationToken);
    Task RequireProfileAsync(PerfilProcessamento profile, CancellationToken cancellationToken);
}

internal interface IProfileEmbeddingProviderFactory
{
    IEmbeddingProvider Create(PerfilProcessamento profile);
}

internal interface IKnowledgeAccess
{
    Task<(string Principal, string? Organization)> RequireAsync(ClaimsPrincipal principal, string permission, CancellationToken cancellationToken);
    /// <summary>Perfil usado pela busca: Retrieval:AdoptedProfileId ou, se omitido, o único perfil configurado.</summary>
    PerfilProcessamento OperationalProfile();
    int QueryLimit { get; }
    TimeSpan QueryTimeout { get; }
}
