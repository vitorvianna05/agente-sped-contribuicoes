using Api.RAG.Domain.Knowledge.Values;

namespace Api.RAG.Domain.Knowledge;

internal sealed record VersaoDocumental(Guid DocumentVersionId, Guid SourceId, string OfficialVersion,
    ContentHash OriginalContentHash, Guid OriginalArtifactId, string PublicationState, Guid? ActiveGenerationId,
    string? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset? WithdrawnAt, Guid Revision, DateTimeOffset CreatedAt)
{
    public void Validate()
    {
        if (DocumentVersionId == Guid.Empty || SourceId == Guid.Empty || OriginalArtifactId == Guid.Empty || Revision == Guid.Empty ||
            string.IsNullOrWhiteSpace(OfficialVersion) || OfficialVersion.Length > 1000 || CreatedAt.Offset != TimeSpan.Zero ||
            PublicationState is not ("unpublished" or "published" or "withdrawn" or "purged") ||
            ((ApprovedBy is null) != (ApprovedAt is null)) || (PublicationState == "published" && (ActiveGenerationId is null || ApprovedAt is null)) ||
            (PublicationState != "published" && ActiveGenerationId is not null) || (PublicationState == "withdrawn" && WithdrawnAt is null) ||
            new[] { ApprovedAt, WithdrawnAt }.Any(date => date is { Offset: var offset } && offset != TimeSpan.Zero) ||
            ApprovedAt < CreatedAt || WithdrawnAt < CreatedAt)
        {
            throw new KnowledgeException("INVALID_STATE");
        }
    }

    public void AssertSameContent(ContentHash hash) => AssertSameContent(OriginalContentHash, hash);

    public static void AssertSameContent(ContentHash originalHash, ContentHash hash)
    {
        if (hash != originalHash)
        {
            throw new KnowledgeException("VERSION_CONTENT_CONFLICT");
        }
    }
}
