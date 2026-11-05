using System.Diagnostics;

namespace Api.RAG.Infrastructure.Observability;

internal sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.TraceIdentifier = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-Id"] = context.TraceIdentifier;
        await next(context);
    }
}
