using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Security;

internal sealed class DevelopmentAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "DevelopmentLocal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var address = Context.Connection.RemoteIpAddress;
        if (address is null || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address))
        {
            return Task.FromResult(AuthenticateResult.Fail("Development requer conexão loopback."));
        }

        var identity = new ClaimsIdentity([
            new Claim("sub", "development-local"),
            new Claim("scope", string.Join(' ', KnowledgeAuthorization.Permissions))
        ], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
