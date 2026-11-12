using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api.Agente.Infrastructure;
using Api.Agente.Tests.Fixtures;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Api.Agente.Tests.Integration;

/// <summary>
/// Skill sofia-cors-rest-a2a: CORS só para navegador de origem pactuada (política SofiaBrowser),
/// chamadas servidor-servidor sem Origin dependem de transporte e JWT, e TLS pode terminar em
/// proxy confiável sem abrir HTTP direto.
/// </summary>
public sealed class CorsTransportTests
{
    private const string Sofia = "https://sofia.synthetic.invalid";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Preflight da origem pactuada recebe permissão; origem desconhecida e health não recebem.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task PreflightAllowsOnlyConfiguredBrowserOrigin()
    {
        await using var host = Host(new() { ["Cors:AllowedOrigins:0"] = Sofia });
        using var client = host.CreateClient();
        using (var allowed = await Preflight(client, "/api/v1/analises", Sofia))
        {
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
            Assert.Equal(Sofia, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Contains("POST", allowed.Headers.GetValues("Access-Control-Allow-Methods").Single(), StringComparison.Ordinal);
            var headers = allowed.Headers.GetValues("Access-Control-Allow-Headers").Single();
            Assert.Contains("authorization", headers, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("content-type", headers, StringComparison.OrdinalIgnoreCase);
            Assert.False(allowed.Headers.Contains("Access-Control-Allow-Credentials"));
        }
        using (var unknown = await Preflight(client, "/api/v1/analises", "https://site-nao-autorizado.example"))
        {
            Assert.False(unknown.Headers.Contains("Access-Control-Allow-Origin"));
        }
        using (var health = await Preflight(client, "/health/live", Sofia))
        {
            Assert.False(health.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    /// <summary>Sem origens configuradas não há CORS; servidor-servidor segue exigindo JWT e permissão.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task WithoutOriginsCorsIsOffAndBackendCallsStillRequireToken()
    {
        await using var host = Host([]);
        using var client = host.CreateClient();
        using (var preflight = await Preflight(client, "/api/v1/analises", Sofia))
        {
            Assert.False(preflight.Headers.Contains("Access-Control-Allow-Origin"));
        }
        using (var anonymous = await Read(client)) { Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); }
        client.DefaultRequestHeaders.Authorization = Bearer(host, "analysis.diagnose");
        using (var forbidden = await Read(client)) { Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); }
        client.DefaultRequestHeaders.Authorization = Bearer(host, "analysis.execute");
        using var allowed = await Read(client);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.False(allowed.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>Requisição real do navegador permitido recebe a origem e expõe a correlação; JWT continua obrigatório.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task BrowserRequestFromAllowedOriginStillNeedsTokenAndExposesCorrelation()
    {
        await using var host = Host(new() { ["Cors:AllowedOrigins:0"] = Sofia });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", Sofia);
        using (var anonymous = await Read(client)) { Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); }
        client.DefaultRequestHeaders.Authorization = Bearer(host, "analysis.execute");
        using var response = await Read(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Sofia, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("X-Correlation-Id", response.Headers.GetValues("Access-Control-Expose-Headers").Single(), StringComparison.Ordinal);
        Assert.Contains("Origin", response.Headers.Vary);
    }

    /// <summary>Fora de Testing, HTTP só passa com X-Forwarded-Proto=https vindo de proxy confiável.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData(null, "https", HttpStatusCode.Forbidden)]
    [InlineData("127.0.0.1", null, HttpStatusCode.Forbidden)]
    [InlineData("127.0.0.1", "http", HttpStatusCode.Forbidden)]
    [InlineData("10.20.30.40", "https", HttpStatusCode.Forbidden)]
    [InlineData("127.0.0.1", "https", HttpStatusCode.Unauthorized)]
    public async Task ForwardedTlsIsHonoredOnlyFromTrustedProxy(string? proxy, string? proto, HttpStatusCode expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["KnowledgeMcp:Endpoint"] = "https://knowledge.synthetic.invalid/mcp",
            ["KnowledgeMcp:AllowUnauthenticatedLoopback"] = "false",
            ["KnowledgeMcp:BearerToken"] = "synthetic-service-token"
        };
        if (proxy is not null) { settings["Transport:TrustedProxies:0"] = proxy; }
        await using var host = Host(settings, "Staging");
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/analises/" + Guid.NewGuid() + "/consumo");
        if (proto is not null) { request.Headers.Add("X-Forwarded-Proto", proto); }
        using var response = await client.SendAsync(request, Token);
        // 401 prova que o transporte foi aceito e a autenticação continua obrigatória.
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Contains("TRANSPORT_DENIED", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }
    }

    /// <summary>Origem com caminho/curinga/HTTP fora de Development e rede aberta são recusadas no startup.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("Production", "https://sofia.example", null, null, true)]
    [InlineData("Production", "https://sofia.example:8443", null, null, true)]
    [InlineData("Development", "http://localhost:4200", null, null, true)]
    [InlineData("Production", "http://sofia.example", null, null, false)]
    [InlineData("Production", "https://sofia.example/", null, null, false)]
    [InlineData("Production", "https://sofia.example/app", null, null, false)]
    [InlineData("Production", "https://*.example", null, null, false)]
    [InlineData("Production", "*", null, null, false)]
    [InlineData("Production", null, "not-an-ip", null, false)]
    [InlineData("Production", null, null, "0.0.0.0/0", false)]
    [InlineData("Production", null, "10.0.0.5", "10.0.0.0/24", true)]
    public void EdgeOptionsRejectOpenOrMalformedValues(string environment, string? origin, string? proxy, string? network, bool valid)
    {
        var options = new AgenteOptions
        {
            Authentication = new() { Authority = "https://identity.synthetic.invalid", Audience = "api-agente", AllowedHosts = ["agente.synthetic.invalid"] },
            Limits = new() { MaxRequestBytes = 1024, TimeoutSeconds = 10 },
            Cors = new() { AllowedOrigins = origin is null ? [] : [origin] },
            Transport = new() { TrustedProxies = proxy is null ? [] : [proxy], TrustedNetworks = network is null ? [] : [network] }
        };
        var result = new AgenteOptionsValidator(new Environment(environment)).Validate(null, options);
        Assert.Equal(valid, result.Succeeded);
    }

    private static AgentTestHost Host(Dictionary<string, string?> settings, string environment = "Testing")
    {
        var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"), environment: environment, settings: settings);
        host.UseKestrel(0);
        return host;
    }

    private static async Task<HttpResponseMessage> Preflight(HttpClient client, string path, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return await client.SendAsync(request, Token);
    }

    private static async Task<HttpResponseMessage> Read(HttpClient client)
    {
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes(AnalysisFixtures.File));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain; charset=iso-8859-1");
        return await client.PostAsync("/api/v1/sped/leitura", content, Token);
    }

    private static AuthenticationHeaderValue Bearer(AgentTestHost host, string scope)
    {
        var jwt = new JwtSecurityToken("https://identity.synthetic.invalid", "api-agente-synthetic",
            [new Claim("sub", "sofia-service"), new Claim("scope", scope)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(host.Key, SecurityAlgorithms.HmacSha256));
        return new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Api.Agente";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
