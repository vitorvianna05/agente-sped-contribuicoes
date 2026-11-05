namespace Api.RAG.Application.Knowledge.Ports;

internal sealed record EmbeddingRequest(Guid OperationId, string CorrelationId, string Text, string Provider, string Model, int Dimensions);
internal sealed record EmbeddingResponse(float[] Vector, long? InputTokens = null, long? OutputTokens = null,
    long? EmbeddingTokens = null, decimal? ProviderReportedCost = null, string? Currency = null);

internal interface IEmbeddingProvider
{
    Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken);
}

internal sealed class EmbeddingTransientException : Exception
{
    public EmbeddingTransientException() : base("DEPENDENCY_UNAVAILABLE") { }
}
