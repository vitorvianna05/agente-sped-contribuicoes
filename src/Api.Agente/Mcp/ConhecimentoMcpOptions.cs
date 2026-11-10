namespace Api.Agente.Mcp;

internal sealed class ConhecimentoMcpOptions
{
    public string? Endpoint { get; set; }
    public string? BearerToken { get; set; }
    public bool AllowUnauthenticatedLoopback { get; set; }
    public int TimeoutSeconds { get; set; }
    public int MaxResponseBytes { get; set; }
    public int MaxQueryCharacters { get; set; }
}
