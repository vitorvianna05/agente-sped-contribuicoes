namespace Api.RAG.Domain.Knowledge;

internal sealed record FonteOficial(Guid SourceId, string Issuer, string OfficialIdentifier, string Title,
    string Kind, Uri OriginUri, DateTimeOffset CreatedAt)
{
    public void Validate()
    {
        if (SourceId == Guid.Empty || Kind is not ("manual" or "guide") || OriginUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(OriginUri.UserInfo) || CreatedAt.Offset != TimeSpan.Zero ||
            new[] { Issuer, OfficialIdentifier, Title }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000))
        {
            throw new KnowledgeException("INVALID_METADATA");
        }
    }
}
