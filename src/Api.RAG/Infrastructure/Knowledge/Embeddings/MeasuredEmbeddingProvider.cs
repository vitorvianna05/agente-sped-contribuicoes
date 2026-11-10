using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Persistence;

namespace Api.RAG.Infrastructure.Knowledge.Embeddings;

internal sealed class MeasuredEmbeddingProvider(IEmbeddingProvider inner, ConsumptionRepository consumption,
    TimeProvider clock, EmbeddingOptions options, ConsumptionPrice? price = null) : IEmbeddingProvider
{
    public async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken)
    {
        if (options.MaxAttempts is not > 0 || options.TimeoutSeconds is not > 0 ||
            options.RetryDelayMilliseconds is not >= 0 || options.SdkAutomaticRetriesEnabled ||
            request.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(request.CorrelationId) ||
            request.Provider != options.Provider || request.Model != options.Model || request.Dimensions != options.Dimensions)
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Persistir intenção antes da rede; falha de persistência impede a chamada faturável.
            var id = await consumption.BeginAsync(request, clock.GetUtcNow(), cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds.Value));
            EmbeddingResponse? response = null;
            try
            {
                response = await inner.GenerateAsync(request, timeout.Token);
                if (response.Vector.Length != request.Dimensions || response.Vector.Any(value => !float.IsFinite(value)))
                {
                    throw new KnowledgeException("PROFILE_INCOMPATIBLE");
                }
            }
            catch (Exception exception) when (exception is EmbeddingTransientException or OperationCanceledException)
            {
                using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds.Value));
                var cancelled = cancellationToken.IsCancellationRequested;
                var status = cancelled ? "cancelled" : exception is OperationCanceledException ? "timeout" : "failed";
                await FinishAsync(id, status, null, recording.Token);
                if (cancelled || attempt >= options.MaxAttempts)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds.Value), cancellationToken);
                continue;
            }
            catch
            {
                using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds.Value));
                await FinishAsync(id, "failed", response, recording.Token);
                throw;
            }

            // Falha neste ponto conserva intenção pendente: desfecho desconhecido, sem retry oculto da chamada.
            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds.Value));
            await FinishAsync(id, "succeeded", response, completion.Token);
            return response;
        }
    }

    private Task FinishAsync(Guid id, string status, EmbeddingResponse? response, CancellationToken cancellationToken)
    {
        var measurement = ConsumptionMeasurement.Create(response?.InputTokens, response?.OutputTokens, response?.EmbeddingTokens,
            response?.ProviderReportedCost, response?.Currency, price);
        return consumption.FinishAsync(id, clock.GetUtcNow(), status, measurement, cancellationToken);
    }
}
