using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Api.Agente.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace Api.Agente.Tests.Integration;

/// <summary>API separada protege diagnóstico, transporte, correlação e tamanho de entrada.</summary>
public sealed class AgentSecurityTests
{
    /// <summary>JWT, permissão e atribuição administrativa antecedem chamadas MCP.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task DiagnosisRequiresTransportIdentityPermissionAndAssignment()
    {
        var token = TestContext.Current.CancellationToken;
        await using var server = await KnowledgeServerProcess.StartAsync("valid", token);
        await using var host = new AgentTestHost(server.Endpoint);
        host.UseKestrel(0);
        using var client = host.CreateClient();
        const string path = "/admin/v1/knowledge/readiness";
        using (var anonymous = await client.GetAsync(path, token)) { Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); }
        foreach (var invalid in new[] { CreateToken(host, "synthetic-admin", "analysis.diagnose", "wrong-audience"), CreateToken(host, "", "analysis.diagnose") })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            using var unauthorized = await client.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }
        foreach (var (subject, scope) in new[] { ("synthetic-admin", "analysis.execute"), ("other-user", "analysis.diagnose") })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(host, subject, scope));
            using var forbidden = await client.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(host, "synthetic-admin", "analysis.diagnose"));
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "client-cannot-assign-system-execution");
        using var success = await client.GetAsync(path, token);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.NotEqual("client-cannot-assign-system-execution", success.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Contains("\"analysisAvailable\":false", await success.Content.ReadAsStringAsync(token), StringComparison.Ordinal);
        using var oversized = new HttpRequestMessage(HttpMethod.Get, path) { Content = new StringContent(new string('x', 2048)) };
        using var rejected = await client.SendAsync(oversized, token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
        client.DefaultRequestHeaders.Host = "untrusted.invalid";
        using var badHost = await client.GetAsync(path, token);
        Assert.Equal(HttpStatusCode.Forbidden, badHost.StatusCode);
    }

    private static string CreateToken(AgentTestHost host, string subject, string scope, string audience = "api-agente-synthetic") => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "https://identity.synthetic.invalid", audience, [new Claim("sub", subject), new Claim("scope", scope)],
        DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(host.Key, SecurityAlgorithms.HmacSha256)));
}
