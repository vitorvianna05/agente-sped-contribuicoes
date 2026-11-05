using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Knowledge.Processing;

internal sealed partial class IngestionWorker(IServiceScopeFactory scopes, IOptions<RagOptions> options,
    TimeProvider clock, ILogger<IngestionWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
        Enumerable.Range(0, options.Value.Processing.MaxConcurrentJobs!.Value).Select(_ => RunAsync(stoppingToken)));

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        // Não inicializar banco/retenção no startup de um ambiente ainda não provisionado.
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionStrings.Rag))
        {
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
                var job = await store.ClaimAsync(clock.GetUtcNow(), TimeSpan.FromSeconds(options.Value.Processing.LeaseSeconds!.Value),
                    options.Value.Processing.MaxPreparationAttempts!.Value, stoppingToken);
                if (job is not null)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds!.Value));
                    // PdfPig é síncrono; executar fora do loop HTTP e conferir cancelamento entre páginas.
                    try
                    {
                        await Task.Run(() => scope.ServiceProvider.GetRequiredService<ProcessarGeracao>().ExecuteAsync(job, deadline.Token), stoppingToken);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds.Value));
                        await store.FinishPreparationAsync(job, [], "OPERATION_TIMEOUT", clock.GetUtcNow(), completion.Token);
                    }
                    catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
                    {
                        WorkerFailure(logger, exception is KnowledgeException domain ? domain.Code : "TECHNICAL_FAILURE");
                        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.Processing.TimeoutSeconds.Value));
                        await store.FinishPreparationAsync(job, [], "TECHNICAL_FAILURE", clock.GetUtcNow(), completion.Token);
                    }
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // Lease persistido permite retomada; não declarar cancelamento definitivo.
            }
            catch (Exception exception)
            {
                WorkerFailure(logger, exception is KnowledgeException domain ? domain.Code : "DEPENDENCY_UNAVAILABLE");
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ingestion worker failure {Code}")]
    private static partial void WorkerFailure(ILogger logger, string code);
}
