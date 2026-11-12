using System.Net;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace Api.RAG.Tests.Contracts;

/// <summary>
/// Publicação atrás de proxy (Render/Vercel, feature 004): HTTP só vira HTTPS com
/// X-Forwarded-Proto vindo de proxy/rede listados; HTTP direto continua negado e JWT segue exigido.
/// </summary>
public sealed class ForwardedTransportTests(FoundationDatabase database) : IClassFixture<FoundationDatabase>
{
    private const string PublicHost = "rag.synthetic.invalid";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>401 prova que o transporte foi aceito e a autenticação continua obrigatória.</summary>
    [Theory, Trait("Category", "Contract")]
    [InlineData(null, null, "https", HttpStatusCode.BadRequest)]
    [InlineData("127.0.0.1", null, null, HttpStatusCode.BadRequest)]
    [InlineData("127.0.0.1", null, "http", HttpStatusCode.BadRequest)]
    [InlineData("10.20.30.40", null, "https", HttpStatusCode.BadRequest)]
    [InlineData(null, "10.0.0.0/8", "https", HttpStatusCode.BadRequest)]
    [InlineData("127.0.0.1", null, "https", HttpStatusCode.Unauthorized)]
    [InlineData(null, "127.0.0.0/8", "https", HttpStatusCode.Unauthorized)]
    public async Task ForwardedTlsIsHonoredOnlyFromTrustedSenders(string? proxy, string? network, string? proto, HttpStatusCode expected)
    {
        await using var host = new RagTestHost(database.Options.ConnectionStrings.Rag);
        var settings = new Dictionary<string, string?> { ["Authentication:AllowedHosts:2"] = PublicHost };
        if (proxy is not null) { settings["Transport:TrustedProxies:0"] = proxy; }
        if (network is not null) { settings["Transport:TrustedNetworks:0"] = network; }
        await using var application = host.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));
        application.UseKestrel(0);
        using var client = application.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Host = PublicHost;
        if (proto is not null) { request.Headers.Add("X-Forwarded-Proto", proto); }
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            Assert.Contains("INVALID_ARGUMENT", await response.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        }
    }

    /// <summary>IP inválido e rede aberta são recusados no startup.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("not-an-ip", null, false)]
    [InlineData(null, "0.0.0.0/0", false)]
    [InlineData(null, "::/0", false)]
    [InlineData(null, "10.0.0.0", false)]
    [InlineData("10.0.0.5", "10.0.0.0/8", true)]
    public void TransportRejectsOpenOrMalformedSenders(string? proxy, string? network, bool valid)
    {
        var options = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-settings.json"))
            .Build().Get<RagOptions>()!;
        options.Transport = new() { TrustedProxies = proxy is null ? [] : [proxy], TrustedNetworks = network is null ? [] : [network] };
        var errors = new RagOptionsValidator(allowSynthetic: true).Validate(null, options);
        Assert.Equal(valid, !errors.Failed || !errors.FailureMessage!.Contains("Transport:", StringComparison.Ordinal));
    }
}
