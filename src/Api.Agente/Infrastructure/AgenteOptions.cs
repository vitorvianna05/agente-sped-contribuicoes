using Api.Agente.Mcp;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Infrastructure;

internal sealed class AgenteOptions
{
    public AgentAuthenticationOptions Authentication { get; set; } = new();
    public AgentLimits Limits { get; set; } = new();
}

internal sealed class AgentAuthenticationOptions
{
    public bool DisabledForDevelopment { get; set; }
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string[] AllowedHosts { get; set; } = [];
    public string[] DiagnosticPrincipals { get; set; } = [];
}

internal sealed class AgentLimits
{
    public long MaxRequestBytes { get; set; }
    public int TimeoutSeconds { get; set; }
}

internal sealed class AgenteOptionsValidator(IHostEnvironment environment) : IValidateOptions<AgenteOptions>, IValidateOptions<ConhecimentoMcpOptions>, IValidateOptions<SpedParserOptions>
{
    public ValidateOptionsResult Validate(string? name, SpedParserOptions options) =>
        options.MaxLineBytes > 0 && options.MaxRecords > 0 && options.MaxFieldsPerRecord >= 2 && options.MaxIssues > 0
            ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("SpedParser exige limites explícitos de linha, registros, campos e ocorrências.");

    public ValidateOptionsResult Validate(string? name, AgenteOptions options)
    {
        var auth = options.Authentication;
        var local = auth.DisabledForDevelopment && environment.IsDevelopment();
        if (auth.DisabledForDevelopment && !environment.IsDevelopment() ||
            !local && (!Uri.TryCreate(auth.Authority, UriKind.Absolute, out var authority) || authority.Scheme != "https" || string.IsNullOrWhiteSpace(auth.Audience)) ||
            auth.AllowedHosts.Length == 0 || auth.AllowedHosts.Any(host => string.IsNullOrWhiteSpace(host) || host.Contains('*', StringComparison.Ordinal)) ||
            options.Limits.MaxRequestBytes <= 0 || options.Limits.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Authentication e Limits devem ser explícitos; dispensa JWT somente em Development.");
        }
        return ValidateOptionsResult.Success;
    }

    public ValidateOptionsResult Validate(string? name, ConhecimentoMcpOptions options)
    {
        var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
            endpoint.AbsolutePath.TrimEnd('/') != "/mcp" ||
            endpoint.Scheme != "https" && !(environment.IsEnvironment("Testing") && endpoint.Scheme == "http" && endpoint.IsLoopback) ||
            options.AllowUnauthenticatedLoopback && (!local || !endpoint.IsLoopback) ||
            !options.AllowUnauthenticatedLoopback && string.IsNullOrWhiteSpace(options.BearerToken) ||
            options.BearerToken?.Any(char.IsWhiteSpace) == true ||
            options.TimeoutSeconds <= 0 || options.MaxResponseBytes <= 0 || options.MaxQueryCharacters <= 0)
        {
            return ValidateOptionsResult.Fail("KnowledgeMcp exige endpoint /mcp HTTPS, credencial externa e limites; exceção sem token apenas loopback local explícito.");
        }
        return ValidateOptionsResult.Success;
    }
}
