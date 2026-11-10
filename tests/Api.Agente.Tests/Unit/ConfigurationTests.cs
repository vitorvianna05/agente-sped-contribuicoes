using Api.Agente.Infrastructure;
using Api.Agente.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Agente.Tests.Unit;

/// <summary>Exceções locais nunca habilitam acesso anônimo ou HTTP fora do ambiente autorizado.</summary>
public sealed class ConfigurationTests
{
    /// <summary>Produção exige JWT e token de serviço, mesmo quando o destino é loopback.</summary>
    [Fact, Trait("Category", "Unit")]
    public void ProductionRejectsAuthenticationBypass()
    {
        var validator = new AgenteOptionsValidator(new TestEnvironment("Production"));
        var options = new AgenteOptions
        {
            Authentication = new AgentAuthenticationOptions { DisabledForDevelopment = true, AllowedHosts = ["localhost"] },
            Limits = new AgentLimits { MaxRequestBytes = 1024, TimeoutSeconds = 5 }
        };
        Assert.True(validator.Validate(null, options).Failed);
        var mcp = Options("https://localhost/mcp");
        Assert.True(validator.Validate(null, mcp).Failed);
        mcp.AllowUnauthenticatedLoopback = false;
        mcp.BearerToken = "synthetic-service-token";
        Assert.True(validator.Validate(null, mcp).Succeeded);
    }

    /// <summary>Modo local é explícito; URL não pode carregar credencial, query ou destino arbitrário.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("https://untrusted.invalid/mcp")]
    [InlineData("http://localhost/mcp")]
    [InlineData("https://user:password@localhost/mcp")]
    [InlineData("https://localhost/mcp?token=bad")]
    [InlineData("https://localhost/other")]
    public void DevelopmentRejectsUnsafeEndpoint(string endpoint) =>
        Assert.True(new AgenteOptionsValidator(new TestEnvironment("Development")).Validate(null, Options(endpoint)).Failed);

    /// <summary>Development exige HTTPS; HTTP sintético é exclusivo do ambiente Testing loopback.</summary>
    [Fact, Trait("Category", "Unit")]
    public void ExplicitLocalConfigurationIsFinite()
    {
        var local = new AgenteOptionsValidator(new TestEnvironment("Development"));
        Assert.True(local.Validate(null, Options("https://localhost/mcp")).Succeeded);
        Assert.True(new AgenteOptionsValidator(new TestEnvironment("Testing")).Validate(null, Options("http://127.0.0.1:12345/mcp")).Succeeded);
        var missingLimits = Options("https://localhost/mcp");
        missingLimits.TimeoutSeconds = 0;
        Assert.True(local.Validate(null, missingLimits).Failed);
    }

    private static ConhecimentoMcpOptions Options(string endpoint) => new()
    {
        Endpoint = endpoint,
        AllowUnauthenticatedLoopback = true,
        TimeoutSeconds = 2,
        MaxResponseBytes = 65536,
        MaxQueryCharacters = 1000
    };

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SyntheticAgentTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
