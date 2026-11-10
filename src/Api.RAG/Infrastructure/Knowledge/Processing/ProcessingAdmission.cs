using Api.RAG.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Knowledge.Processing;

internal sealed class ProcessingAdmission(IOptions<RagOptions> options) : IDisposable
{
    private readonly SemaphoreSlim slots = new(options.Value.Processing.MaxConcurrentJobs!.Value);
    public async Task<IAsyncDisposable> EnterAsync(CancellationToken token)
    {
        await slots.WaitAsync(token);
        return new Lease(slots);
    }
    public void Dispose() => slots.Dispose();
    private sealed class Lease(SemaphoreSlim slots) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            slots.Release();
            return ValueTask.CompletedTask;
        }
    }
}
