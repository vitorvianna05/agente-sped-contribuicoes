using Api.Agente.Analise;
using Api.Agente.Infrastructure.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Agente.Controllers;

/// <summary>Adaptador REST do fluxo especializado; SOFIA/A2A reutilizará o caso de uso.</summary>
[ApiController, Route("api/v1/analises"), Authorize(Policy = "analysis.execute")]
public sealed class AnalisesController : ControllerBase
{
    /// <summary>Analisa arquivo Latin-1 enviado diretamente no corpo, sem identidade no payload.</summary>
    /// <param name="token">Cancelamento HTTP.</param>
    /// <returns>Validações, evidências, parecer validado quando disponível e limitações.</returns>
    [HttpPost, Consumes("text/plain", "application/octet-stream"), OpenApiSpedUpload]
    public async Task<IActionResult> Analyze(CancellationToken token)
    {
        var charset = Request.GetTypedHeaders().ContentType?.Charset.Value;
        if (!string.IsNullOrWhiteSpace(charset) && !charset.Trim('"').Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase))
        { throw new AnalysisException("ENCODING_NOT_SUPPORTED"); }
        Response.Headers.CacheControl = "no-store";
        var result = await HttpContext.RequestServices.GetRequiredService<AnaliseSpedService>().AnalyzeAsync(Request.Body, Identity(), token);
        return Ok(result);
    }

    /// <summary>Retorna apenas consumo da execução pertencente ao solicitante/contexto autenticado.</summary>
    /// <param name="id">Execução local.</param>
    /// <param name="token">Cancelamento HTTP.</param>
    /// <returns>Tentativas e métricas conhecidas, sem arquivo, prompt ou parecer.</returns>
    [HttpGet("{id:guid}/consumo")]
    public async Task<IActionResult> Consumption(Guid id, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var identity = Identity();
        var attempts = await HttpContext.RequestServices.GetRequiredService<IAnalysisLedger>().ReadAsync(id, identity.PrincipalId, identity.OrganizationId, token);
        return Ok(new
        {
            contractVersion = "1.0",
            executionId = id,
            correlationId = identity.CorrelationId,
            attempts,
            attemptCount = attempts.Length,
            unknownCostAttempts = attempts.Count(item => item.EstimatedCost is null && item.ProviderReportedCost is null),
            byModel = attempts.GroupBy(attempt => attempt.Model).Select(group => new
            {
                model = group.Key,
                attemptCount = group.Count(),
                knownInputTokens = group.Any(attempt => attempt.InputTokens.HasValue) ? group.Sum(attempt => attempt.InputTokens) : null,
                knownOutputTokens = group.Any(attempt => attempt.OutputTokens.HasValue) ? group.Sum(attempt => attempt.OutputTokens) : null,
                unknownUsageAttempts = group.Count(attempt => attempt.InputTokens is null || attempt.OutputTokens is null)
            })
        });
    }

    private ExecutionIdentity Identity()
    {
        var subject = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) { throw new AnalysisException("IDENTITY_REQUIRED"); }
        return new((Guid)HttpContext.Items["ExecutionId"]!, HttpContext.TraceIdentifier, subject, User.FindFirst("organization_id")?.Value);
    }
}
