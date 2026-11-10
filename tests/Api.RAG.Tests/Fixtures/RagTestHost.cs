using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Api.RAG.Tests.Fixtures;

internal sealed class RagTestHost(string? connectionString = null) : WebApplicationFactory<Program>
{
    public SymmetricSecurityKey SigningKey { get; } = new(RandomNumberGenerator.GetBytes(64));
    public ControlledTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseUrls("http://127.0.0.1:0");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "synthetic-settings.json"));
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Rag"] = connectionString,
                ["ConnectionStrings:RagMigration"] = null,
                ["Processing:EmbeddingStageEnabled"] = "false"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var metadata = new OpenIdConnectConfiguration { Issuer = "https://identity.synthetic.invalid" };
                metadata.SigningKeys.Add(SigningKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                options.TokenValidationParameters.IssuerSigningKey = SigningKey;
                options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
            });
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<SyntheticEmbeddingProvider>();
            services.ConfigureHttpClientDefaults(client =>
                client.ConfigurePrimaryHttpMessageHandler(() => new RejectOutboundHandler()));
        });
    }
}
