using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Knowledge.Processing;

internal sealed partial class EmbeddingWorker(IServiceScopeFactory scopes, IOptions<RagOptions> options, TimeProvider clock, ProcessingAdmission admission,
    ILogger<EmbeddingWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.EmbeddingStageActive || string.IsNullOrWhiteSpace(options.Value.ConnectionStrings.Rag))
        {
            EmbeddingStageInactive(logger);
            return Task.CompletedTask;
        }

        return Task.WhenAll(Enumerable.Range(0, options.Value.Processing.MaxConcurrentJobs!.Value).Select(_ => RunAsync(stoppingToken)));
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var admissionLease = await admission.EnterAsync(stoppingToken);
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IKnowledgeStore>();
                var job = await store.ClaimEmbeddingAsync(clock.GetUtcNow(), TimeSpan.FromSeconds(options.Value.Processing.LeaseSeconds!.Value),
                    options.Value.Processing.MaxPreparationAttempts!.Value, stoppingToken);
                if (job is not null)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds!.Value));
                    try { await scope.ServiceProvider.GetRequiredService<ProcessarEmbeddings>().ExecuteAsync(job, deadline.Token); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds.Value));
                        var code = exception is OperationCanceledException ? "OPERATION_TIMEOUT" : exception is KnowledgeException domain ? domain.Code : "DEPENDENCY_UNAVAILABLE";
                        await store.CompleteEmbeddingAsync(job, code, clock.GetUtcNow(), completion.Token);
                    }
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { Failure(logger, exception is KnowledgeException domain ? domain.Code : "DEPENDENCY_UNAVAILABLE"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Etapa vetorial inativa: requer ConnectionStrings:Rag e chave OpenAI (OpenAI:ApiKey) ou Processing:EmbeddingStageEnabled=true.")]
    private static partial void EmbeddingStageInactive(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Embedding worker failure {Code}")]
    private static partial void Failure(ILogger logger, string code);
}
