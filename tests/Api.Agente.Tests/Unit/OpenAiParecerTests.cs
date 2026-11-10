using System.ClientModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Agente.Llm;
using Api.Agente.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Api.Agente.Tests.Unit;

/// <summary>SDK real em HTTP simulado: sem chave real ou cobrança OpenAI.</summary>
public sealed class OpenAiParecerTests
{
    /// <summary>Structured Outputs e uso reportado são preservados pelo adaptador.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task AdapterSendsStrictSchemaAndPreservesUsage()
    {
        var context = ParecerValidationTests.Context;
        using var handler = new Handler(HttpStatusCode.OK, AnalysisFixtures.Draft(context));
        var options = AnalysisFixtures.Llm; options.ApiKey = "synthetic-not-a-real-key";
        var provider = new OpenAiParecerLlm(Options.Create(options), new Factory(handler));
        var result = await provider.GenerateAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal("succeeded", result.State);
        Assert.Equal(12L, result.InputTokens); Assert.Equal(7L, result.OutputTokens);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("json_schema", json.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.True(json.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean());
        Assert.Equal(options.Model, json.RootElement.GetProperty("model").GetString());
        Assert.DoesNotContain(options.ApiKey, handler.Body!, StringComparison.Ordinal);
    }

    /// <summary>429 não causa retry oculto do SDK.</summary>
    [Fact, Trait("Category", "Unit")]
    public async Task AdapterDoesNotRetryOnItsOwn()
    {
        using var handler = new Handler(HttpStatusCode.TooManyRequests, "");
        var options = AnalysisFixtures.Llm; options.ApiKey = "synthetic";
        var provider = new OpenAiParecerLlm(Options.Create(options), new Factory(handler));
        await Assert.ThrowsAsync<ClientResultException>(() => provider.GenerateAsync(ParecerValidationTests.Context, TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>Ausência real de contador não é confundida com zero pelo DTO do SDK.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("absent")]
    [InlineData("empty")]
    public async Task MissingProviderUsageStaysUnknown(string usage)
    {
        using var handler = new Handler(HttpStatusCode.OK, AnalysisFixtures.Draft(ParecerValidationTests.Context), usage);
        var options = AnalysisFixtures.Llm; options.ApiKey = "synthetic";
        var result = await new OpenAiParecerLlm(Options.Create(options), new Factory(handler)).GenerateAsync(ParecerValidationTests.Context, TestContext.Current.CancellationToken);
        Assert.Null(result.InputTokens); Assert.Null(result.OutputTokens);
    }

    /// <summary>Recusa e truncamento são distintos e preservam tokens reportados.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("refused")]
    [InlineData("incomplete")]
    public async Task UnusableCompletionStillReportsKnownUsage(string state)
    {
        using var handler = new Handler(HttpStatusCode.OK, AnalysisFixtures.Draft(ParecerValidationTests.Context), completionState: state);
        var options = AnalysisFixtures.Llm; options.ApiKey = "synthetic";
        var result = await new OpenAiParecerLlm(Options.Create(options), new Factory(handler)).GenerateAsync(ParecerValidationTests.Context, TestContext.Current.CancellationToken);
        Assert.Equal(state, result.State); Assert.Equal(12L, result.InputTokens); Assert.Equal(7L, result.OutputTokens);
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class Handler(HttpStatusCode status, string draft, string usage = "known", string completionState = "succeeded") : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var body = status == HttpStatusCode.OK ? JsonSerializer.Serialize(new
            {
                id = "chatcmpl-synthetic",
                @object = "chat.completion",
                created = 0,
                model = "synthetic-model",
                choices = new[] { new { index = 0, message = new { role = "assistant", content = draft }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 12, completion_tokens = 7, total_tokens = 19 }
            }) : """{"error":{"message":"synthetic","type":"rate_limit_error","code":"rate_limit_exceeded"}}""";
            if (status == HttpStatusCode.OK && usage != "known")
            {
                var node = JsonNode.Parse(body)!.AsObject();
                if (usage == "absent") { node.Remove("usage"); } else { node["usage"] = new JsonObject(); }
                body = node.ToJsonString();
            }
            if (status == HttpStatusCode.OK && completionState != "succeeded")
            {
                var node = JsonNode.Parse(body)!;
                if (completionState == "refused")
                {
                    node["choices"]![0]!["message"]!["content"] = null;
                    node["choices"]![0]!["message"]!["refusal"] = "Synthetic refusal";
                }
                else { node["choices"]![0]!["finish_reason"] = "length"; }
                body = node.ToJsonString();
            }
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
