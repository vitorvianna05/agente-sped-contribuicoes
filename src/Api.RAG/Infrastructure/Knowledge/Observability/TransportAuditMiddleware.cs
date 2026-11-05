using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Knowledge.Observability;

internal sealed class TransportAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IKnowledgeUnitOfWorkFactory factory, TimeProvider clock,
        IOptions<RagOptions> configured, IHostEnvironment environment)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp") && !context.Request.Path.StartsWithSegments("/admin") &&
            !context.Request.Path.StartsWithSegments("/api/v1/ingest"))
        {
            await next(context);
            return;
        }

        // Sem política operacional, bloquear antes de executar; não inventar retenção para auditar.
        RagOptionsValidator.RequirePersistence(configured.Value, environment.IsEnvironment("Testing") &&
            configured.Value.ConfigurationPurpose == "SyntheticTestsOnly_NotApprovedForProduction");
        var responseBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            try
            {
                await next(context);
            }
            catch (Exception exception)
            {
                using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.Processing.TimeoutSeconds!.Value));
                var result = exception switch
                {
                    KnowledgeException domain => domain.Code,
                    OperationCanceledException => "cancelled",
                    _ => "technical_failure"
                };
                await RecordAsync(result, recording.Token);
                throw;
            }

            using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.Processing.TimeoutSeconds!.Value));
            await RecordAsync(context.Response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture), completion.Token);
            buffer.Position = 0;
            await buffer.CopyToAsync(responseBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = responseBody;
        }

        async Task RecordAsync(string result, CancellationToken cancellationToken)
        {
            var actor = AuthenticatedActor.TryFrom(context.User);
            await using var unit = await factory.BeginAsync(cancellationToken);
            await unit.Audit.WriteAsync(new AuditEntry(Guid.NewGuid(), actor?.PrincipalId ?? "anonymous",
                actor?.OrganizationId, context.Request.Path.StartsWithSegments("/mcp") ? "mcp.request" : "admin.request",
                null, result, clock.GetUtcNow(), context.TraceIdentifier), cancellationToken);
            await unit.CommitAsync(cancellationToken);
        }
    }
}
