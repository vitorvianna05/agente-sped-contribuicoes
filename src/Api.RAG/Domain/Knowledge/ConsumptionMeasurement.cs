namespace Api.RAG.Domain.Knowledge;

internal sealed record ConsumptionMeasurement
{
    private ConsumptionMeasurement() { }

    public long? InputTokens { get; private init; }
    public long? OutputTokens { get; private init; }
    public long? EmbeddingTokens { get; private init; }
    public string UsageAvailability { get; private init; } = "unavailable";
    public decimal? EstimatedCost { get; private init; }
    public string? EstimatedCurrency { get; private init; }
    public decimal? ProviderReportedCost { get; private init; }
    public string? Currency { get; private init; }
    public string? PriceReference { get; private init; }
    public string? PriceVersion { get; private init; }

    public static ConsumptionMeasurement Create(long? inputTokens, long? outputTokens, long? embeddingTokens,
        decimal? providerReportedCost, string? providerCurrency, ConsumptionPrice? price)
    {
        if (new[] { inputTokens, outputTokens, embeddingTokens }.Any(value => value is < 0) || providerReportedCost is < 0)
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }

        if (providerCurrency is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerCurrency);
        }

        return new ConsumptionMeasurement
        {
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EmbeddingTokens = embeddingTokens,
            UsageAvailability = inputTokens.HasValue || outputTokens.HasValue || embeddingTokens.HasValue ? "available" : "unavailable",
            EstimatedCost = embeddingTokens is { } tokens && price is not null ? tokens * price.PerMillionEmbeddingTokens / 1_000_000m : null,
            EstimatedCurrency = price?.Currency,
            ProviderReportedCost = providerReportedCost,
            Currency = providerCurrency,
            PriceReference = price?.Reference,
            PriceVersion = price?.Version
        };
    }
}
