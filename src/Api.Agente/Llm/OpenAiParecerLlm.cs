using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Api.Agente.Analise;
using Api.Agente.Contracts;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace Api.Agente.Llm;

internal sealed class OpenAiParecerLlm(IOptions<LlmOptions> configured, IHttpClientFactory clients) : IParecerLlm
{
    private static readonly Lazy<string> Prompt = new(() => Resource("Api.Agente.Parecer.Prompt.v1"));
    private static readonly Lazy<string> Schema = new(() => Resource("Api.Agente.Parecer.Schema.v1"));
    public async Task<LlmResponse> GenerateAsync(ParecerContext context, CancellationToken cancellationToken)
    {
        var options = configured.Value;
        if (string.IsNullOrWhiteSpace(options.ApiKey)) { throw new AnalysisException("LLM_NOT_CONFIGURED"); }
        var payload = JsonSerializer.Serialize(context, AnalysisJson.Options);
        if (payload.Length > options.MaxContextCharacters) { throw new AnalysisException("LLM_CONTEXT_TOO_LARGE"); }
        using var http = clients.CreateClient("OpenAIParecer");
        var client = new ChatClient(options.Model, new ApiKeyCredential(options.ApiKey), new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(0),
            Transport = new HttpClientPipelineTransport(http)
        });
        var response = await client.CompleteChatAsync([new SystemChatMessage(Prompt.Value), new UserChatMessage(payload)],
            new ChatCompletionOptions
            {
                MaxOutputTokenCount = options.MaxOutputTokens,
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("parecer_v1", BinaryData.FromString(Schema.Value), jsonSchemaIsStrict: true)
            }, cancellationToken);
        var completion = response.Value;
        // O DTO do SDK usa int: só aceitar contadores realmente presentes no JSON do provedor.
        using var raw = JsonDocument.Parse(response.GetRawResponse().Content.ToString());
        var usage = raw.RootElement.TryGetProperty("usage", out var reported) && reported.ValueKind == JsonValueKind.Object ? reported : default;
        var state = !string.IsNullOrEmpty(completion.Refusal) ? "refused" : completion.FinishReason == ChatFinishReason.Stop ? "succeeded" : "incomplete";
        var text = completion.Content.Count == 1 ? completion.Content[0].Text : null;
        var details = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("prompt_tokens_details", out var nested) &&
            nested.ValueKind == JsonValueKind.Object ? nested : default;
        return new LlmResponse(text, Tokens(usage, "prompt_tokens"), Tokens(usage, "completion_tokens"), state, Tokens(details, "cached_tokens"));

        static long? Tokens(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var count) && count >= 0 ? count : null;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(OpenAiParecerLlm).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
