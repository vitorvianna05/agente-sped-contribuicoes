using Api.Agente.Mcp;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Infrastructure;

internal sealed class AgenteOptions
{
    public AgentAuthenticationOptions Authentication { get; set; } = new();
    public AgentLimits Limits { get; set; } = new();
    public AgentCorsOptions Cors { get; set; } = new();
    public AgentTransportOptions Transport { get; set; } = new();
}

/// <summary>Origens de navegador (frontend SOFIA) autorizadas; vazio = CORS desligado (chamadas servidor-servidor não usam CORS).</summary>
internal sealed class AgentCorsOptions
{
    public const string Policy = "SofiaBrowser";
    public string[] AllowedOrigins { get; set; } = [];
}

/// <summary>Proxies que terminam TLS e encaminham X-Forwarded-Proto/For; vazio = cabeçalhos encaminhados ignorados.</summary>
internal sealed class AgentTransportOptions
{
    public string[] TrustedProxies { get; set; } = [];
    public string[] TrustedNetworks { get; set; } = [];
    public bool TrustsForwardedHeaders => TrustedProxies.Length > 0 || TrustedNetworks.Length > 0;
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
        var origins = options.Cors.AllowedOrigins ?? [];
        if (origins.Any(origin => !ValidOrigin(origin, environment.IsDevelopment())) ||
            origins.Distinct(StringComparer.OrdinalIgnoreCase).Count() != origins.Length)
        {
            return ValidateOptionsResult.Fail("Cors:AllowedOrigins aceita apenas origens explícitas (esquema://host[:porta], sem caminho nem curinga), HTTPS fora de Development.");
        }
        var transport = options.Transport;
        if ((transport.TrustedProxies ?? []).Any(proxy => !System.Net.IPAddress.TryParse(proxy, out _)) ||
            (transport.TrustedNetworks ?? []).Any(network => !System.Net.IPNetwork.TryParse(network, out var parsed) || parsed.PrefixLength == 0))
        {
            return ValidateOptionsResult.Fail("Transport:TrustedProxies exige IPs e Transport:TrustedNetworks exige CIDR específicos (nunca 0.0.0.0/0 ou ::/0).");
        }
        return ValidateOptionsResult.Success;
    }

    // Origem = esquema + host + porta. Sem caminho, consulta, fragmento, credenciais ou curinga.
    private static bool ValidOrigin(string? origin, bool development) =>
        !string.IsNullOrWhiteSpace(origin) && !origin.Contains('*', StringComparison.Ordinal) && !origin.EndsWith('/') &&
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.AbsolutePath == "/" && uri.Query.Length == 0 &&
        uri.Fragment.Length == 0 && uri.UserInfo.Length == 0 &&
        (uri.Scheme == Uri.UriSchemeHttps || development && uri.Scheme == Uri.UriSchemeHttp) &&
        string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);

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
