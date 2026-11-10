using System.Text.Json;
using Api.RAG.Tools;

namespace Api.RAG.Tests.Contracts;

/// <summary>Catálogo anunciado pelo handler SDK usa os schemas completos do contrato versionado.</summary>
public sealed class McpSchemaTests
{
    /// <summary>Sem refs locais pendentes; entradas e saídas rejeitam propriedades extras.</summary>
    [Fact, Trait("Category", "Contract")]
    public async Task SchemasAreCompleteAndStrict()
    {
        var catalog = await KnowledgeMcpTools.ListAsync(null!, TestContext.Current.CancellationToken);
        Assert.Equal(2, catalog.Tools.Count);
        foreach (var tool in catalog.Tools)
        {
            Assert.DoesNotContain("$ref", tool.InputSchema.GetRawText(), StringComparison.Ordinal);
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
            var output = tool.OutputSchema!.Value;
            Assert.DoesNotContain("$ref", output.GetRawText(), StringComparison.Ordinal);
            Assert.False(output.GetProperty("additionalProperties").GetBoolean());
            Assert.Contains(output.GetProperty("required").EnumerateArray(), value => value.GetString() == "contractVersion");
            Assert.Contains(output.GetProperty("required").EnumerateArray(), value => value.GetString() == "limitations");
            Assert.Equal(JsonValueKind.Object, output.ValueKind);
        }
        var search = Assert.Single(catalog.Tools, tool => tool.Name == "BuscarConhecimento");
        Assert.Equal(3, search.InputSchema.GetProperty("required").GetArrayLength());
        Assert.True(search.InputSchema.GetProperty("properties").TryGetProperty("versaoLayout", out _));
    }
}
