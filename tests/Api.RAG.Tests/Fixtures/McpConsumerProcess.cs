using System.Diagnostics;
using System.Text.Json;
using Api.RAG.Endpoints.Contracts;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Api.RAG.Tests.Fixtures;

internal sealed record McpConsumerRequest(Uri Endpoint, string? ToolName, Dictionary<string, object?>? Arguments, string? BearerToken = null);
internal sealed record McpConsumerResult(int ProcessId, string[] Tools, CallToolResult? Result);

internal static class McpConsumerProcess
{
    public static async Task<McpConsumerResult> ExecuteAsync(
        McpConsumerRequest request, CancellationToken cancellationToken,
        TimeSpan? timeout = null, Action<int>? processStarted = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executionTimeout = timeout ?? TimeSpan.FromSeconds(30);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(executionTimeout, TimeSpan.Zero);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(McpConsumerProcess).Assembly.Location);
        start.ArgumentList.Add("--mcp-consumer");
        // Nenhum token, conexão ou configuração do usuário é herdado pelo consumidor sintético.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PATH", "SystemRoot", "WINDIR", "TEMP", "TMP", "DOTNET_ROOT"
        };
        foreach (var key in start.Environment.Keys.Where(key => !allowed.Contains(key)).ToArray())
        {
            start.Environment.Remove(key);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Consumidor MCP não iniciou.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(executionTimeout);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            processStarted?.Invoke(process.Id);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Consumidor MCP falhou ({process.ExitCode}): {await error}");
            }

            return JsonSerializer.Deserialize<McpConsumerResult>(await output)
                ?? throw new InvalidOperationException("Consumidor MCP não retornou resultado.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            await Task.WhenAll(output, error);
        }
    }

    public static async Task<int> RunChildAsync()
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var line = await Console.In.ReadLineAsync(deadline.Token);
            var request = JsonSerializer.Deserialize<McpConsumerRequest>(line ?? "")
                ?? throw new InvalidOperationException("Pedido ausente.");
            if (!request.Endpoint.IsLoopback || request.Endpoint.Scheme != Uri.UriSchemeHttp)
            {
                throw new InvalidOperationException("O consumidor sintético aceita somente HTTP loopback.");
            }

            await using var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = request.Endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(5),
                AdditionalHeaders = request.BearerToken is null ? null : new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {request.BearerToken}"
                }
            });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: deadline.Token);
            var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
            foreach (var tool in tools.Where(tool => tool.Name is "BuscarConhecimento" or "ResolverReferencia"))
            {
                var schema = tool.ProtocolTool;
                if (schema.OutputSchema is not { } output || schema.InputSchema.GetProperty("additionalProperties").GetBoolean() ||
                    output.GetProperty("additionalProperties").GetBoolean() || schema.InputSchema.GetRawText().Contains("$ref", StringComparison.Ordinal) ||
                    output.GetRawText().Contains("$ref", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid fiscal tool schema");
                }
            }
            var result = request.ToolName is null ? null : await client.CallToolAsync(
                request.ToolName, request.Arguments, cancellationToken: deadline.Token);
            if (request.ToolName is "BuscarConhecimento" or "ResolverReferencia")
            {
                var text = result!.Content.OfType<TextContentBlock>().Single().Text;
                var parsed = ContractValidator.ReadResult(text);
                using var document = JsonDocument.Parse(text);
                if (result.StructuredContent is not { } structured || !JsonElement.DeepEquals(document.RootElement, structured) ||
                    (result.IsError == true) != (parsed.Status == "technical_failure"))
                {
                    throw new InvalidOperationException("Invalid fiscal tool result");
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(new McpConsumerResult(
                Environment.ProcessId, tools.Select(tool => tool.Name).ToArray(), result)));
            return 0;
        }
        catch (Exception exception)
        {
            // Não devolver mensagem do transporte: ela pode conter payload/cabeçalhos futuros.
            Console.Error.WriteLine(exception.GetType().Name);
            return 1;
        }
    }
}
