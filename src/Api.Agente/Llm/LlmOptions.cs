namespace Api.Agente.Llm;

internal sealed class LlmOptions
{
    public string Model { get; set; } = "gpt-4.1-mini";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; }
    public int MaxAttempts { get; set; }
    public int RetryDelayMilliseconds { get; set; }
    public int MaxOutputTokens { get; set; }
    public int MaxContextCharacters { get; set; }
    public int MaxOutputCharacters { get; set; }
}
