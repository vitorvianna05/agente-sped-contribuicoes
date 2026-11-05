using System.Net;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infrastructure.Security;

internal sealed class TransportGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IOptions<RagOptions> options, IHostEnvironment environment)
    {
        var auth = options.Value.Authentication;
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
        {
            bodyLimit.MaxRequestBodySize = options.Value.Processing.MaxFileBytes;
        }

        if (context.Request.ContentLength > options.Value.Processing.MaxFileBytes)
        {
            throw new KnowledgeException("FILE_TOO_LARGE");
        }
        var host = context.Request.Host.Host;
        var synthetic = environment.IsEnvironment("Testing") &&
            options.Value.ConfigurationPurpose == "SyntheticTestsOnly_NotApprovedForProduction";
        var loopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
        if (!auth.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase) ||
            (!context.Request.IsHttps && !(synthetic && loopback)))
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }

        if (context.Request.Headers.TryGetValue("Origin", out var origin) &&
            (origin.Count != 1 || !auth.AllowedOrigins.Contains(origin.ToString(), StringComparer.Ordinal)))
        {
            throw new KnowledgeException("PERMISSION_DENIED");
        }

        await next(context);
    }
}
