using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Api.Agente.Tests.Fixtures;

internal sealed class AgentTestHost(Uri endpoint, Action<IServiceCollection>? configure = null, string environment = "Testing",
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    internal SymmetricSecurityKey Key { get; } = new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseUrls("http://127.0.0.1:0");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Authority"] = "https://identity.synthetic.invalid",
                ["Authentication:Audience"] = "api-agente-synthetic",
                ["Authentication:AllowedHosts:0"] = "127.0.0.1",
                ["Authentication:AllowedHosts:1"] = "localhost",
                ["Authentication:DiagnosticPrincipals:0"] = "synthetic-admin",
                ["SpedParser:MaxLineBytes"] = "512",
                ["SpedParser:MaxRecords"] = "100",
                ["SpedParser:MaxFieldsPerRecord"] = "32",
                ["SpedParser:MaxIssues"] = "20",
                ["Llm:Model"] = "gpt-4.1-mini",
                ["Llm:TimeoutSeconds"] = "5",
                ["Llm:MaxAttempts"] = "2",
                ["Llm:RetryDelayMilliseconds"] = "0",
                ["Llm:MaxOutputTokens"] = "1000",
                ["Llm:MaxContextCharacters"] = "32000",
                ["Llm:MaxOutputCharacters"] = "16000",
                ["AgentData:TimeoutSeconds"] = "5",
                ["AgentData:RetentionBatchSize"] = "10",
                ["Limits:MaxRequestBytes"] = "1024",
                ["Limits:TimeoutSeconds"] = "30",
                ["KnowledgeMcp:Endpoint"] = endpoint.AbsoluteUri,
                ["KnowledgeMcp:AllowUnauthenticatedLoopback"] = "true",
                ["KnowledgeMcp:MaxResponseBytes"] = "65536",
                ["KnowledgeMcp:TimeoutSeconds"] = "15",
                ["KnowledgeMcp:MaxQueryCharacters"] = "1000"
            });
            if (settings is not null) { config.AddInMemoryCollection(settings); }
        });
        builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
        {
            var metadata = new OpenIdConnectConfiguration { Issuer = "https://identity.synthetic.invalid" };
            metadata.SigningKeys.Add(Key);
            jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
            jwt.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        }));
        if (configure is not null) { builder.ConfigureTestServices(configure); }
    }
}
