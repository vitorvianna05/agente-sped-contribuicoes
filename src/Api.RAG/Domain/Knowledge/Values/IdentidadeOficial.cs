namespace Api.RAG.Domain.Knowledge.Values;

internal sealed record IdentidadeOficial
{
    public IdentidadeOficial(string issuer, string officialIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(officialIdentifier);
        Issuer = issuer;
        OfficialIdentifier = officialIdentifier;
    }

    public string Issuer { get; }
    public string OfficialIdentifier { get; }
}
