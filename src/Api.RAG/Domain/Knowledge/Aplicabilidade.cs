namespace Api.RAG.Domain.Knowledge;

internal sealed record Aplicabilidade(string DocumentType, string LayoutVersion, string OfficialBasis,
    string RecordedBy, DateTimeOffset RecordedAt)
{
    public void Validate()
    {
        if (new[] { DocumentType, LayoutVersion, OfficialBasis, RecordedBy }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1000) ||
            RecordedAt.Offset != TimeSpan.Zero)
        {
            throw new KnowledgeException("INVALID_METADATA");
        }
    }
}
