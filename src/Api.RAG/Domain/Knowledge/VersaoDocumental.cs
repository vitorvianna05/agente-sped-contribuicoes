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

    /// <summary>
    /// Publicação automática: a geração concluída passa a ser a ativa da versão (primeira publicação ou substituição
    /// após reprocessamento). Aprovação manual não existe; ApprovedBy registra quem enviou a primeira geração publicada.
    /// </summary>
    public VersaoDocumental PublishGeneration(GeracaoProcessamento generation, string actor, DateTimeOffset at)
    {
        if (PublicationState == "purged" || generation.DocumentVersionId != DocumentVersionId || generation.State != "succeeded" ||
            generation.ContentManifestHash is null || generation.SupersededAt is not null)
        {
            throw new KnowledgeException("INVALID_STATE");
        }

        var result = this with
        {
            PublicationState = "published",
            ActiveGenerationId = generation.GenerationId,
            ApprovedBy = ApprovedBy ?? actor,
            ApprovedAt = ApprovedAt ?? at,
            WithdrawnAt = null,
            Revision = Guid.NewGuid()
        };
        result.Validate();
        return result;
    }

    /// <summary>Retira a versão da busca (substituída por outra versão oficial da mesma fonte).</summary>
    public VersaoDocumental Withdraw(DateTimeOffset at)
    {
        if (PublicationState is not ("unpublished" or "published"))
        {
            throw new KnowledgeException("INVALID_STATE");
        }

        var result = this with { PublicationState = "withdrawn", WithdrawnAt = at, ActiveGenerationId = null, Revision = Guid.NewGuid() };
        result.Validate();
        return result;
    }

    public static void AssertSameContent(ContentHash originalHash, ContentHash hash)
    {
        if (hash != originalHash)
        {
            throw new KnowledgeException("VERSION_CONTENT_CONFLICT");
        }
    }
}
