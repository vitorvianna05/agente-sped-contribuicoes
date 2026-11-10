using System.Text.Json;
using Api.Agente.Mcp.Contracts;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Api.Agente.Mcp;

internal sealed class ConhecimentoMcpClient(IHttpClientFactory clients, IOptions<ConhecimentoMcpOptions> configured) : IConhecimentoClient
{
    public Task<string[]> DiscoverAsync(string correlationId, CancellationToken cancellationToken) =>
        ExecuteAsync(correlationId, (_, tools, _) => Task.FromResult(tools), cancellationToken);

    public Task<KnowledgeResult> SearchAsync(string query, string documentType, string layoutVersion, string correlationId, CancellationToken cancellationToken)
    {
        if (new[] { query, documentType, layoutVersion }.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > configured.Value.MaxQueryCharacters))
        {
            throw new KnowledgeClientException("KNOWLEDGE_CONTEXT_REQUIRED");
        }
        return ExecuteAsync(correlationId, async (client, _, token) =>
        {
            var result = Read(await client.CallToolAsync("BuscarConhecimento", new Dictionary<string, object?>
            {
                ["consulta"] = query,
                ["tipoDocumento"] = documentType,
                ["versaoLayout"] = layoutVersion
            }, cancellationToken: token));
            if (result.Evidences.Any(evidence => evidence.Applicability.DocumentType != documentType ||
                evidence.Applicability.LayoutVersion != layoutVersion || evidence.ReferenceState != "active"))
            {
                throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_INVALID");
            }
            return result;
        }, cancellationToken);
    }

    public Task<KnowledgeResult> ResolveAsync(Guid referenceId, string correlationId, CancellationToken cancellationToken)
    {
        if (referenceId == Guid.Empty)
        {
            throw new KnowledgeClientException("KNOWLEDGE_REFERENCE_INVALID");
        }
        return ExecuteAsync(correlationId, async (client, _, token) =>
        {
            var result = Read(await client.CallToolAsync("ResolverReferencia", new Dictionary<string, object?>
            {
                ["referenceId"] = referenceId.ToString()
            }, cancellationToken: token));
            if (result.Evidences.Length > 1 || result.Evidences.Any(evidence => evidence.ReferenceId != referenceId))
            {
                throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_INVALID");
            }
            return result;
        }, cancellationToken);
    }

    private static KnowledgeResult Read(CallToolResult result)
    {
        if (result.Content.Count != 1 || result.Content[0] is not TextContentBlock text)
        {
            throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_INVALID");
        }
        var parsed = KnowledgeContract.ReadResult(text.Text);
        using var json = JsonDocument.Parse(text.Text);
        if (result.StructuredContent is { } structured && !JsonElement.DeepEquals(json.RootElement, structured) ||
            (result.IsError == true) != (parsed.Status == "technical_failure"))
        {
            throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_INVALID");
        }
        if (parsed.Status == "technical_failure")
        {
            throw new KnowledgeClientException("KNOWLEDGE_REMOTE_FAILURE", parsed.CorrelationId);
        }
        return parsed;
    }

    private async Task<T> ExecuteAsync<T>(string correlation, Func<McpClient, string[], CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(correlation) || correlation.Length > 128 || correlation.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new KnowledgeClientException("CORRELATION_INVALID");
        }
        var options = configured.Value;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            using var http = clients.CreateClient("KnowledgeMcp");
            var headers = new Dictionary<string, string> { ["X-Correlation-Id"] = correlation };
            if (!string.IsNullOrWhiteSpace(options.BearerToken))
            {
                headers.Add("Authorization", $"Bearer {options.BearerToken}");
            }
            await using var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(options.Endpoint!),
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                MaxReconnectionAttempts = 0,
                ConnectionTimeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                AdditionalHeaders = headers
            }, http);
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: deadline.Token);
            var tools = await client.ListToolsAsync(cancellationToken: deadline.Token);
            KnowledgeToolCatalog.Validate(tools.Select(tool => tool.ProtocolTool).ToArray());
            return await action(client, KnowledgeToolCatalog.Definitions.Select(tool => tool.Name).ToArray(), deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (KnowledgeClientException) { throw; }
        catch (JsonException) { throw new KnowledgeClientException("KNOWLEDGE_RESPONSE_INVALID"); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is KnowledgeClientException known)
                {
                    throw new KnowledgeClientException(known.Code, known.RemoteCorrelationId);
                }
            }
            throw new KnowledgeClientException(deadline.IsCancellationRequested ? "KNOWLEDGE_TIMEOUT" : "KNOWLEDGE_UNAVAILABLE");
        }
    }
}
