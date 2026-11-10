using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Agente.Infrastructure.Security;

internal static class AnalysisAuthorization
{
    private static readonly string[] Permissions = ["analysis.execute", "analysis.diagnose"];
    public static void AddAnalysisAuthorization(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var local = environment.IsDevelopment() && configuration.GetValue<bool>("Authentication:DisabledForDevelopment");
        if (local)
        {
            services.AddAuthentication(LocalAgentAuthentication.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, LocalAgentAuthentication>(LocalAgentAuthentication.SchemeName, _ => { });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<AgenteOptions>>((jwt, configured) =>
            {
                jwt.Authority = configured.Value.Authentication.Authority;
                jwt.Audience = configured.Value.Authentication.Audience;
                jwt.RequireHttpsMetadata = true;
                jwt.MapInboundClaims = false;
                jwt.IncludeErrorDetails = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub"
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Principal?.FindFirst("sub")?.Value))
                        {
                            context.Fail("Subject required.");
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        }
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions)
            {
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                {
                    var subject = context.User.FindFirst("sub")?.Value;
                    var scopes = context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
                        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    if (string.IsNullOrWhiteSpace(subject) || !scopes.Contains(permission, StringComparer.Ordinal))
                    {
                        return false;
                    }
                    return permission != "analysis.diagnose" ||
                        local && context.User.Identity?.AuthenticationType == LocalAgentAuthentication.SchemeName ||
                        (configuration.GetSection("Authentication:DiagnosticPrincipals").Get<string[]>() ?? []).Contains(subject, StringComparer.Ordinal);
                }));
            }
        });
    }
}

internal sealed class LocalAgentAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "AgentDevelopmentLocal";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ip = Context.Connection.RemoteIpAddress;
        if (ip is null || !IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))
        {
            return Task.FromResult(AuthenticateResult.Fail("Loopback required."));
        }
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "development-local"), new Claim("scope", "analysis.execute analysis.diagnose")
        ], SchemeName)), SchemeName)));
    }
}
