using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Persistence;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Knowledge.Embeddings;

internal sealed class ProfileEmbeddingProviderFactory(IEmbeddingProvider provider, ConsumptionRepository consumption,
    TimeProvider clock, IOptions<RagOptions> configured) : IProfileEmbeddingProviderFactory
{
    public IEmbeddingProvider Create(PerfilProcessamento profile)
    {
        profile.Validate();
        var options = configured.Value.Embeddings;
        return new MeasuredEmbeddingProvider(provider, consumption, clock, new EmbeddingOptions
        {
            Provider = profile.EmbeddingProvider,
            Model = profile.EmbeddingModel,
            Dimensions = profile.EmbeddingDimensions,
            TimeoutSeconds = options.TimeoutSeconds,
            MaxAttempts = options.MaxAttempts,
            RetryDelayMilliseconds = options.RetryDelayMilliseconds,
            SdkAutomaticRetriesEnabled = false
        }, profile.EmbeddingProvider == "OpenAI" ? configured.Value.OpenAI.Pricing.ToEmbeddingPrice() : null);
    }
}
