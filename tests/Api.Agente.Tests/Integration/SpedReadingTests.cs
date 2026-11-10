using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Agente.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace Api.Agente.Tests.Integration;

/// <summary>Leitura HTTP de fixtures sintéticas, sem servidor MCP, IA ou persistência.</summary>
public sealed class SpedReadingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Path = "/api/v1/sped/leitura";

    /// <summary>Permissão de execução é obrigatória; campos lidos não são conclusão fiscal.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task FileReadingRequiresPermissionAndReturnsExplicitLimitations()
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        host.UseKestrel(0);
        using var client = host.CreateClient();
        using (var anonymous = await SendAsync(client, "|0000|006|\r\n|9999|2|\r\n"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }
        Authenticate(client, host, "analysis.diagnose");
        using (var forbidden = await SendAsync(client, "|0000|006|\r\n|9999|2|\r\n"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }
        Authenticate(client, host, "analysis.execute");
        using var success = await SendAsync(client, "|0000|006|\r\n|Z999|João||0001|\r\n|9999|3|\r\n");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.True(success.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await success.Content.ReadAsStringAsync(Token));
        var root = json.RootElement;
        Assert.NotEqual(Guid.Empty, root.GetProperty("executionId").GetGuid());
        Assert.Equal("partial", root.GetProperty("status").GetString());
        Assert.Equal("completed", root.GetProperty("readingStatus").GetString());
        Assert.Equal("not_performed", root.GetProperty("fiscalValidationStatus").GetString());
        Assert.Equal(4, root.GetProperty("checksNotPerformed").GetArrayLength());
        var document = root.GetProperty("document");
        Assert.Equal("006", document.GetProperty("declaredLayoutCode").GetString());
        Assert.Equal("Z999", document.GetProperty("records")[1].GetProperty("code").GetString());
        Assert.Equal("João", document.GetProperty("records")[1].GetProperty("fields")[0].GetString());
        Assert.Empty(document.GetProperty("issues").EnumerateArray());
    }

    /// <summary>Falhas estruturais retornam ocorrências localizadas e readingStatus inválido.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task StructuralIssuesDoNotBecomeSuccessfulValidation()
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        host.UseKestrel(0);
        using var client = host.CreateClient();
        Authenticate(client, host, "analysis.execute");
        using var response = await SendAsync(client, "|0000|006|\n|9999|2|\n");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("failed", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("invalid", json.RootElement.GetProperty("readingStatus").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("document").GetProperty("issues").GetArrayLength());
    }

    /// <summary>Tipo, codificação, tamanho e arquivo vazio falham com códigos técnicos sem dados fiscais.</summary>
    [Theory, Trait("Category", "Integration")]
    [InlineData("charset", 415, "SPED_ENCODING_NOT_SUPPORTED")]
    [InlineData("empty", 422, "SPED_FILE_EMPTY")]
    [InlineData("size", 413, "REQUEST_TOO_LARGE")]
    [InlineData("line", 413, "SPED_LINE_TOO_LONG")]
    public async Task InvalidUploadNeverReturnsPartialRecordsAsSuccess(string scenario, int status, string code)
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        host.UseKestrel(0);
        using var client = host.CreateClient();
        Authenticate(client, host, "analysis.execute");
        var body = scenario switch { "empty" => "", "size" => new string('x', 2048), "line" => new string('x', 600), _ => "|0000|006|\r\n|9999|2|\r\n" };
        using var response = await SendAsync(client, body, scenario == "charset" ? "text/plain; charset=utf-8" : "application/octet-stream");
        Assert.Equal(status, (int)response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains(code, text, StringComparison.Ordinal);
        Assert.DoesNotContain("rawLine", text, StringComparison.Ordinal);
        Assert.Contains("correlationId", text, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string text, string contentType = "text/plain; charset=iso-8859-1")
    {
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes(text));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return await client.PostAsync(Path, content, Token);
    }

    private static void Authenticate(HttpClient client, AgentTestHost host, string scope)
    {
        var jwt = new JwtSecurityToken("https://identity.synthetic.invalid", "api-agente-synthetic",
            [new Claim("sub", "synthetic-admin"), new Claim("scope", scope)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(host.Key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
    }

    /// <summary>Corpo chunked sem Content-Length também respeita o limite de entrada.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ChunkedUploadCannotBypassFileLimit()
    {
        await using var host = new AgentTestHost(new Uri("http://127.0.0.1:1/mcp"));
        host.UseKestrel(0);
        using var client = host.CreateClient();
        Authenticate(client, host, "analysis.execute");
        using var content = new ChunkedContent();
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.PostAsync(Path, content, Token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("REQUEST_TOO_LARGE", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    private sealed class ChunkedContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(new byte[2048], CancellationToken.None).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
