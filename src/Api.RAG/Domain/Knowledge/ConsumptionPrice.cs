namespace Api.RAG.Domain.Knowledge;

internal sealed record ConsumptionPrice
{
    public ConsumptionPrice(decimal perMillionEmbeddingTokens, string currency, string reference, string version)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(perMillionEmbeddingTokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        PerMillionEmbeddingTokens = perMillionEmbeddingTokens;
        Currency = currency;
        Reference = reference;
        Version = version;
    }

    public decimal PerMillionEmbeddingTokens { get; }
    public string Currency { get; }
    public string Reference { get; }
    public string Version { get; }
}
