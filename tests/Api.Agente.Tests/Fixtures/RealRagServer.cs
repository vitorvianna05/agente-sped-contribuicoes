using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace Api.Agente.Tests.Fixtures;

/// <summary>
/// Api.RAG real em outro processo (modo <c>--rag-server</c> de Api.RAG.Tests): Kestrel, MCP, JWT e
/// PostgreSQL/PgVector próprios, com um PDF sintético publicado e embeddings sintéticos.
/// </summary>
public sealed class RealRagServer : IAsyncLifetime
{
    private Process? process;
    private Task? pump;
    private readonly Channel<string> acks = Channel.CreateUnbounded<string>();
    private readonly TaskCompletionSource<string> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Endpoint MCP da RAG (loopback HTTP, ambiente Testing).</summary>
    public Uri Endpoint { get; private set; } = null!;
    /// <summary>JWT de serviço com knowledge.read, emitido pela identidade sintética da RAG.</summary>
    public string Token { get; private set; } = null!;

    /// <summary>Inicia o processo e aguarda o documento sintético publicado.</summary>
    public async ValueTask InitializeAsync()
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(RagTestsAssembly());
        start.ArgumentList.Add("--rag-server");
        foreach (var key in start.Environment.Keys.Where(key => key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("DOTNET_ENVIRONMENT", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            start.Environment.Remove(key);
        }
        process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        pump = Task.Run(async () =>
        {
            // Drena a saída inteira (logs do Testcontainers incluídos) para o filho nunca bloquear.
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("RAG-SERVER ", StringComparison.Ordinal)) { ready.TrySetResult(line["RAG-SERVER ".Length..]); }
                else if (line == "RAG-ACK") { await acks.Writer.WriteAsync(line); }
            }
            ready.TrySetException(new InvalidOperationException("Api.RAG process exited: " + await error));
        });
        using var started = JsonDocument.Parse(await ready.Task.WaitAsync(TimeSpan.FromMinutes(3)));
        Endpoint = new Uri(started.RootElement.GetProperty("endpoint").GetString()!);
        Token = started.RootElement.GetProperty("token").GetString()!;
    }

    /// <summary>Liga/desliga a falha do provedor de embeddings da RAG e aguarda a confirmação.</summary>
    public async Task SetEmbeddingFailureAsync(bool fail, CancellationToken token)
    {
        await process!.StandardInput.WriteLineAsync((fail ? "fail" : "ok").AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
        await acks.Reader.ReadAsync(token).AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
    }

    /// <summary>Fecha a entrada do filho, que encerra a RAG e descarta o contêiner.</summary>
    public async ValueTask DisposeAsync()
    {
        if (process is null) { return; }
        try { process.StandardInput.Close(); } catch (IOException) { }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        if (pump is not null) { await pump; }
        process.Dispose();
    }

    // Api.RAG.Tests é compilado junto (ProjectReference sem assembly) na mesma configuração/TFM.
    private static string RagTestsAssembly()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var framework = here.Name; var configuration = here.Parent!.Name;
        var path = Path.Combine(here.Parent.Parent!.Parent!.Parent!.FullName, "Api.RAG.Tests", "bin", configuration, framework, "Api.RAG.Tests.dll");
        return File.Exists(path) ? path : throw new FileNotFoundException("Api.RAG.Tests não compilado.", path);
    }
}
