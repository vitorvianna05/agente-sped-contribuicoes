using System.Security.Cryptography;
using System.Text;

namespace Api.RAG.Tests.Fixtures;

internal sealed class ControlledTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset currentUtc = initialUtc.ToUniversalTime();

    public override DateTimeOffset GetUtcNow() => currentUtc;

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        currentUtc += elapsed;
    }
}

// Dublê de infraestrutura; T012/T029 definirão a porta e o adaptador de produção.
internal sealed class SyntheticEmbeddingProvider
{
    private int calls;

    public int Calls => Volatile.Read(ref calls);

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref calls);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Task.FromResult(new[] { (hash[0] + 1) / 256f, (hash[1] + 1) / 256f, (hash[2] + 1) / 256f });
    }
}

internal sealed class RejectOutboundHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Rede externa não é permitida no host determinístico.");
}
