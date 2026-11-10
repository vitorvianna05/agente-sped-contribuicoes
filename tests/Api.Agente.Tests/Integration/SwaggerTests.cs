using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Agente.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Agente.Tests.Integration;

/// <summary>OpenAPI/Swagger locais documentam o contrato real, sem depender de RAG, OpenAI ou banco.</summary>
public sealed class SwaggerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>JWT é por operação; uploads são arquivos binários, preservando o contrato bruto.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task DocumentDescribesBinaryUploadsActualRoutesAndBearer()
    {
        await using var host = Host(); host.UseKestrel(0); using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var root = json.RootElement;
        Assert.Contains("Api.Agente", root.GetProperty("info").GetProperty("title").GetString()!, StringComparison.Ordinal);
        Assert.Equal("bearer", root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = root.GetProperty("paths");
        foreach (var path in new[] { "/api/v1/analises", "/api/v1/sped/leitura" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            var body = operation.GetProperty("requestBody");
            Assert.True(body.GetProperty("required").GetBoolean());
            var content = body.GetProperty("content");
            Assert.False(content.TryGetProperty("multipart/form-data", out _));
            var schema = content.GetProperty("application/octet-stream").GetProperty("schema");
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.Equal("binary", schema.GetProperty("format").GetString());
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        }
        Assert.True(paths.TryGetProperty("/api/v1/analises/{id}/consumo", out _));
        Assert.True(paths.TryGetProperty("/admin/v1/knowledge/readiness", out _));
        var health = paths.GetProperty("/health/live").GetProperty("get");
        Assert.True(!health.TryGetProperty("security", out var security) || security.GetArrayLength() == 0);
        Assert.Contains("AUDIT_NOT_CONFIGURED", paths.GetProperty("/api/v1/analises").GetProperty("post").GetProperty("description").GetString()!, StringComparison.Ordinal);
        Assert.True(paths.GetProperty("/api/v1/analises").GetProperty("post").GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
    }

    /// <summary>UI e recursos são servidos localmente e permanecem sob o guard de transporte.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task UiAndAssetsAreAvailableOnlyOnAllowedTransport()
    {
        await using var host = Host(); host.UseKestrel(0); using var client = host.CreateClient();
        using var ui = await client.GetAsync("/swagger/index.html", Token);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Contains("Api.Agente", await ui.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        using var asset = await client.GetAsync("/swagger/swagger-ui-bundle.js", Token);
        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        client.DefaultRequestHeaders.Host = "untrusted.invalid";
        using var deniedUi = await client.GetAsync("/swagger/index.html", Token);
        using var deniedDocument = await client.GetAsync("/openapi/v1.json", Token);
        Assert.Equal(HttpStatusCode.Forbidden, deniedUi.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedDocument.StatusCode);
    }

    /// <summary>Modo local documentado permite testar leitura Latin-1 sem dependências de análise.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task LocalModeDoesNotRequireJwtAndPreservesUploadedBytes()
    {
        await using var host = Host(local: true); host.UseKestrel(0); using var client = host.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Token));
        Assert.True(!document.RootElement.TryGetProperty("components", out var components) || !components.TryGetProperty("securitySchemes", out _));
        var mediaType = document.RootElement.GetProperty("paths").GetProperty("/api/v1/sped/leitura").GetProperty("post")
            .GetProperty("requestBody").GetProperty("content").EnumerateObject().First().Name;
        using var content = new ByteArrayContent(Encoding.Latin1.GetBytes("|0000|006|\r\n|Z999|João||0001|\r\n|9999|3|\r\n"));
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        using var response = await client.PostAsync("/api/v1/sped/leitura", content, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var reading = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Equal("João", reading.RootElement.GetProperty("document").GetProperty("records")[1].GetProperty("fields")[0].GetString());
        Assert.Equal("not_performed", reading.RootElement.GetProperty("fiscalValidationStatus").GetString());
    }

    /// <summary>Adicionar Swagger não publica UI ou documento em ambiente normal.</summary>
    [Fact, Trait("Category", "Integration")]
    public async Task ProductionDoesNotExposeUiOrDocument()
    {
        await using var host = Host(environment: "Production"); host.UseKestrel(0); using var client = host.CreateClient();
        foreach (var path in new[] { "/swagger/index.html", "/openapi/v1.json" })
        {
            using var response = await client.GetAsync(path, Token);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private static AgentTestHost Host(bool local = false, string environment = "Development") => new(new Uri("https://127.0.0.1:1/mcp"),
        services =>
        {
            services.AddSingleton<IStartupFilter, HttpsTestRequests>();
            if (!local && environment == "Development")
            {
                // Configuração do factory chega após a seleção do modo local no host; escolhe JWT explicitamente para este cenário.
                services.AddAuthentication("SwaggerTestBearer").AddJwtBearer("SwaggerTestBearer");
            }
        }, environment,
        new Dictionary<string, string?>
        {
            ["Authentication:DisabledForDevelopment"] = local.ToString(),
            ["KnowledgeMcp:AllowUnauthenticatedLoopback"] = (environment == "Development").ToString(),
            ["KnowledgeMcp:BearerToken"] = environment == "Development" ? null : "synthetic-service-token"
        });

    // Apenas no host de teste: simula requisição HTTPS; não relaxa o guard de produção.
    private sealed class HttpsTestRequests : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) => { context.Request.Scheme = "https"; return continuation(); });
            next(app);
        };
    }
}
