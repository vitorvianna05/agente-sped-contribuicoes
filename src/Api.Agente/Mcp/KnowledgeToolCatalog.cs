using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Api.Agente.Mcp;

internal static class KnowledgeToolCatalog
{
    private static readonly Lazy<Tool[]> Expected = new(() =>
    {
        using var stream = typeof(KnowledgeToolCatalog).Assembly.GetManifestResourceStream("Api.Agente.Knowledge.Schema.json")!;
        var root = JsonNode.Parse(stream)!;
        return root["x-tools"]!.AsArray().Select(node => new Tool
        {
            Name = node!["name"]!.GetValue<string>(),
            InputSchema = JsonSerializer.SerializeToElement(Expand(node["inputSchema"]!, root)),
            OutputSchema = JsonSerializer.SerializeToElement(Expand(node["outputSchema"]!, root))
        }).ToArray();
    });

    internal static Tool[] Definitions => Expected.Value;

    public static void Validate(IReadOnlyList<Tool> tools)
    {
        foreach (var expected in Definitions)
        {
            var found = tools.Where(tool => tool.Name == expected.Name).ToArray();
            if (found.Length != 1 || found[0].OutputSchema is not { } output ||
                !JsonElement.DeepEquals(found[0].InputSchema, expected.InputSchema) ||
                !JsonElement.DeepEquals(output, expected.OutputSchema!.Value))
            {
                throw new KnowledgeClientException("KNOWLEDGE_SCHEMA_INCOMPATIBLE");
            }
        }
    }

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
            return new JsonArray(array.Select(item => item is null ? null : Expand(item, root)).ToArray());
        }
        return node.DeepClone();
    }
}
