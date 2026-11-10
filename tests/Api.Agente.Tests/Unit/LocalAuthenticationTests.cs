using System.Net;
using Api.Agente.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Agente.Tests.Unit;

/// <summary>Dispensa local de JWT nunca autentica conexões externas.</summary>
public sealed class LocalAuthenticationTests
{
    /// <summary>Somente loopback recebe a identidade local com permissões explícitas.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("192.0.2.10", false)]
    [InlineData(null, false)]
    public async Task DevelopmentAuthenticationIsRestrictedToLoopback(string? address, bool succeeds)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:DisabledForDevelopment"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalysisAuthorization(configuration, new LocalEnvironment());
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        var result = await context.AuthenticateAsync();
        Assert.Equal(succeeds, result.Succeeded);
        if (succeeds)
        {
            Assert.Equal("development-local", result.Principal!.FindFirst("sub")!.Value);
            Assert.True((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(result.Principal, null, "analysis.diagnose")).Succeeded);
        }
    }

    private sealed class LocalEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SyntheticAgentTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
