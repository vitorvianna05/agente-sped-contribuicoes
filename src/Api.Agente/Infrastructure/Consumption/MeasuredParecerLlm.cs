using System.ClientModel;
using Api.Agente.Analise;
using Api.Agente.Contracts;
using Api.Agente.Llm;
using Microsoft.Extensions.Options;

namespace Api.Agente.Infrastructure.Consumption;

internal sealed class MeasuredParecerLlm(IParecerLlm provider, IAnalysisLedger ledger, IOptions<LlmOptions> configured)
{
    public async Task<ParecerDraft> GenerateAsync(ExecutionIdentity identity, ParecerContext context, CancellationToken token)
    {
        var options = configured.Value;
        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var id = await ledger.BeginAttemptAsync(identity, options.Model, "parecer-v1", token);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            LlmResponse? response = null;
            ParecerDraft? draft = null;
            Exception? failure = null;
            try
            {
                response = await provider.GenerateAsync(context, timeout.Token);
                if (response.State != "succeeded" || response.Json is null) { throw new AnalysisException("LLM_" + response.State.ToUpperInvariant()); }
                draft = ParecerValidator.Read(response.Json, context, options.MaxOutputCharacters);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException) { failure = exception; }
            using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            // Falha ao salvar mantém intenção pending e impede nova tentativa automática.
            await ledger.FinishAttemptAsync(id, token.IsCancellationRequested ? "cancelled" : failure is OperationCanceledException ? "timeout" : failure is null ? "succeeded" : "failed", response, recording.Token);
            token.ThrowIfCancellationRequested();
            if (failure is null) { return draft!; }
            var retry = failure is OperationCanceledException or AnalysisException { Code: "PARECER_INVALID" } ||
                failure is ClientResultException { Status: 408 or 429 or >= 500 };
            if (!retry || attempt == options.MaxAttempts)
            {
                throw new AnalysisException(failure is AnalysisException known ? known.Code : failure is OperationCanceledException ? "LLM_TIMEOUT" : "LLM_UNAVAILABLE");
            }
            await Task.Delay(options.RetryDelayMilliseconds, token);
        }
        throw new AnalysisException("LLM_NOT_CONFIGURED");
    }
}
