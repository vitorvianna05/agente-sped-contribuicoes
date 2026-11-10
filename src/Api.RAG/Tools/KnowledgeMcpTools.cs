using System.Text.Json;
using System.Text.Json.Nodes;
using Api.RAG.Endpoints.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Api.RAG.Tools;

internal static class KnowledgeMcpTools
{
    private static readonly Lazy<Tool[]> Catalog = new(() =>
    {
        using var stream = typeof(KnowledgeMcpTools).Assembly.GetManifestResourceStream("Api.RAG.Tools.Schema.json")!;
        var catalog = JsonNode.Parse(stream)!;
        return catalog["x-tools"]!.AsArray().Select(node => new Tool
        {
            Name = node!["name"]!.GetValue<string>(),
            Description = node["name"]!.GetValue<string>() == "BuscarConhecimento" ? "Recupera evidências da versão aplicável. Não conclui conformidade fiscal." : "Resolve o trecho exato de referência, inclusive histórico retido.",
            InputSchema = JsonSerializer.SerializeToElement(Expand(node["inputSchema"]!, catalog)),
            OutputSchema = JsonSerializer.SerializeToElement(Expand(node["outputSchema"]!, catalog))
        }).ToArray();
    });

    private static JsonNode? Expand(JsonNode node, JsonNode root)
    {
        if (node is JsonObject obj)
        {
            if (obj["$ref"] is { } reference)
            {
                return Expand(root["$defs"]![reference.GetValue<string>().Split('/')[^1]]!, root);
            }

            var result = new JsonObject();
            foreach (var pair in obj)
            {
                result[pair.Key] = pair.Value is null ? null : Expand(pair.Value, root);
            }

            return result;
        }
        if (node is JsonArray array)
        {
            return new JsonArray(array.Select(value => value is null ? null : Expand(value, root)).ToArray());
        }

        return node.DeepClone();
    }

    public static ValueTask<ListToolsResult> ListAsync(RequestContext<ListToolsRequestParams> request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ListToolsResult { Tools = Catalog.Value.ToList() });
    }

    public static async ValueTask<CallToolResult> CallAsync(RequestContext<CallToolRequestParams> request, CancellationToken token)
    {
        var parameters = request.Params ?? throw new McpProtocolException("Invalid parameters", McpErrorCode.InvalidParams);
        var arguments = parameters.Arguments ?? throw new McpProtocolException("Arguments required", McpErrorCode.InvalidParams);
        var services = request.Services!;
        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext ?? throw new McpProtocolException("Transport context required", McpErrorCode.InvalidRequest);
        string Read(string name) => arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text : throw new McpProtocolException("Invalid arguments", McpErrorCode.InvalidParams);
        KnowledgeResult result;
        switch (parameters.Name)
        {
            case "BuscarConhecimento" when arguments.Count == 3 && arguments.Keys.All(name => name is "consulta" or "tipoDocumento" or "versaoLayout"):
                result = await services.GetRequiredService<BuscarConhecimentoTool>().ExecuteAsync(Read("consulta"), Read("tipoDocumento"), Read("versaoLayout"), request.User ?? http.User, http.TraceIdentifier, token);
                break;
            case "ResolverReferencia" when arguments.Count == 1 && arguments.ContainsKey("referenceId"):
                if (!Guid.TryParse(Read("referenceId"), out var id) || id == Guid.Empty)
                {
                    throw new McpProtocolException("Invalid reference", McpErrorCode.InvalidParams);
                }

                result = await services.GetRequiredService<ResolverReferenciaTool>().ExecuteAsync(id, request.User ?? http.User, http.TraceIdentifier, token);
                break;
            case "BuscarConhecimento" or "ResolverReferencia": throw new McpProtocolException("Invalid arguments", McpErrorCode.InvalidParams);
            default: throw new McpProtocolException("Unknown tool", McpErrorCode.InvalidParams);
        }
        var json = JsonSerializer.Serialize(result, ContractValidator.JsonOptions);
        ContractValidator.ReadResult(json);
        return new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(result, ContractValidator.JsonOptions),
            Content = [new TextContentBlock { Text = json }],
            IsError = result.Status == "technical_failure"
        };
    }
}
