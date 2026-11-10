using System.Net;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.RAG.Tests.Unit;

/// <summary>O modo sem JWT é exclusivo do desenvolvimento local.</summary>
public sealed class DevelopmentAuthenticationTests
{
    /// <summary>O ambiente deve autorizar explicitamente a dispensa de JWT.</summary>
    [Fact, Trait("Category", "Unit")]
    public void DevelopmentFlagCannotEnableProductionBypass()
    {
        var options = new ConfigurationBuilder().AddJsonFile(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "synthetic-settings.json")).Build().Get<RagOptions>()!;
        options.Authentication.DisabledForDevelopment = true;
        options.Authentication.Authority = null;
        options.Authentication.Audience = null;
        Assert.True(new RagOptionsValidator(allowSynthetic: true, allowDevelopment: true).Validate(null, options).Succeeded);
        Assert.Contains(new RagOptionsValidator(allowSynthetic: true).Validate(null, options).Failures!,
            failure => failure.Contains("DisabledForDevelopment", StringComparison.Ordinal));
    }

    /// <summary>Somente conexões locais recebem identidade de desenvolvimento.</summary>
    [Theory, Trait("Category", "Unit")]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("192.0.2.10", false)]
    [InlineData(null, false)]
    public async Task AuthenticationRequiresLoopback(string? address, bool succeeds)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:DisabledForDevelopment"] = "true"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKnowledgeAuthorization(configuration, allowDevelopment: true);
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        var result = await context.AuthenticateAsync();
        Assert.Equal(succeeds, result.Succeeded);
        if (succeeds)
        {
            Assert.Equal("development-local", result.Principal!.FindFirst("sub")!.Value);
            var authorization = provider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
            Assert.True((await authorization.AuthorizeAsync(result.Principal, null, "knowledge.publish")).Succeeded);
        }
    }
}
