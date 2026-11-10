using Api.RAG.Endpoints.Middleware;
using Api.RAG.Endpoints.OpenApi;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Knowledge.Observability;
using Api.RAG.Infrastructure.Observability;
using Api.RAG.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Api.RAG.Endpoints;

internal static class KnowledgeEndpointRegistration
{
    private static readonly string[] BearerMethods = ["header"];
    public static void MapKnowledgeFoundation(this WebApplication app)
    {
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseMiddleware<ProblemDetailsHandler>();
        app.UseRequestTimeouts();
        app.UseStatusCodePages(async status =>
        {
            var code = status.HttpContext.Response.StatusCode switch
            {
                401 => "AUTHENTICATION_REQUIRED",
                403 => "PERMISSION_DENIED",
                404 => "RESOURCE_NOT_FOUND",
                _ => "INVALID_ARGUMENT"
            };
            await ProblemDetailsHandler.WriteAsync(status.HttpContext, status.HttpContext.Response.StatusCode, code);
        });
        app.UseMiddleware<TransportAuditMiddleware>();
        app.UseMiddleware<TransportGuardMiddleware>();
        if (app.Environment.IsDevelopment())
        {
            // Swagger UI somente em Development; continua sujeito a HTTPS e AllowedHosts/AllowedOrigins.
            app.UseKnowledgeSwaggerUi();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGroup("/admin/v1").RequireAuthorization("knowledge.audit");
        app.MapMcp("/mcp").RequireAuthorization("knowledge.read");
        app.MapGet("/.well-known/oauth-protected-resource/mcp", (IOptions<RagOptions> configured) => configured.Value.Authentication.DisabledForDevelopment
            ? (IResult)Results.NotFound() : Results.Json(new
            {
                resource = configured.Value.Authentication.Resource,
                authorization_servers = new[] { configured.Value.Authentication.Authority },
                scopes_supported = KnowledgeAuthorization.Permissions,
                bearer_methods_supported = BearerMethods
            })).AllowAnonymous();
    }
}
