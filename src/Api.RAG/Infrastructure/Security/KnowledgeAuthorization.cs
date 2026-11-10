using Api.RAG.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api.RAG.Infrastructure.Security;

internal static class KnowledgeAuthorization
{
    public static readonly string[] Permissions = ["knowledge.read", "knowledge.ingest", "knowledge.publish",
        "knowledge.reprocess", "knowledge.withdraw", "knowledge.expurge", "knowledge.audit"];

    public static IServiceCollection AddKnowledgeAuthorization(this IServiceCollection services, IConfiguration configuration, bool allowDevelopment = false)
    {
        var localAuthentication = allowDevelopment && configuration.GetValue<bool>("Authentication:DisabledForDevelopment");
        if (localAuthentication)
        {
            services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                    DevelopmentAuthenticationHandler.SchemeName, _ => { });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
            });
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<Microsoft.Extensions.Options.IOptions<RagOptions>>((jwt, configured) =>
                {
                    var auth = configured.Value.Authentication;
                    jwt.Authority = auth.Authority;
                    jwt.Audience = auth.Audience;
                    jwt.RequireHttpsMetadata = true;
                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidIssuer = auth.Authority,
                        ValidateAudience = true,
                        ValidAudience = auth.Audience,
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
                            if (context.Principal is null || AuthenticatedActor.TryFrom(context.Principal) is null)
                            {
                                context.Fail("Subject required.");
                            }

                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            context.Response.Headers.WWWAuthenticate =
                                $"Bearer resource_metadata=\"{new Uri(new Uri(auth.Resource!), "/.well-known/oauth-protected-resource/mcp").AbsoluteUri}\"";
                            return Task.CompletedTask;
                        }
                    };
                });
        }
        services.AddAuthorizationBuilder();
        foreach (var permission in Permissions)
        {
            services.AddAuthorization(options => options.AddPolicy(permission, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context =>
                {
                    var actor = AuthenticatedActor.TryFrom(context.User);
                    var scopes = context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
                        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    if (actor is null || !scopes.Contains(permission, StringComparer.Ordinal))
                    {
                        return false;
                    }

                    if (permission == "knowledge.read" || (localAuthentication &&
                        context.User.Identity?.AuthenticationType == DevelopmentAuthenticationHandler.SchemeName))
                    {
                        return true;
                    }

                    // Atribuição central externa; nenhum papel organizacional concede esse poder.
                    var assignments = configuration.GetSection("Authentication:CentralPermissionAssignments").Get<CentralAssignment[]>() ?? [];
                    return assignments.Any(assignment => assignment.PrincipalId == actor.PrincipalId && assignment.Permissions.Contains(permission, StringComparer.Ordinal));
                })));
        }

        return services;
    }
}
