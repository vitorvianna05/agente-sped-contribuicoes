using Api.Agente.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Agente.Controllers;

/// <summary>Diagnóstico técnico restrito da conexão MCP; não executa análise fiscal.</summary>
[ApiController, Route("admin/v1/knowledge"), Authorize(Policy = "analysis.diagnose")]
public sealed class KnowledgeDiagnosticsController : ControllerBase
{
    /// <summary>Confere descoberta e contratos das duas tools obrigatórias.</summary>
    /// <param name="token">Cancelamento HTTP.</param>
    /// <returns>Metadados técnicos, sem conteúdo documental ou parecer.</returns>
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(CancellationToken token)
    {
        var tools = await HttpContext.RequestServices.GetRequiredService<IConhecimentoClient>().DiscoverAsync(HttpContext.TraceIdentifier, token);
        return Ok(new
        {
            contractVersion = "1.0",
            correlationId = HttpContext.TraceIdentifier,
            executionId = (Guid)HttpContext.Items["ExecutionId"]!,
            transportReady = true,
            tools,
            analysisAvailable = false,
            fiscalReadiness = "not_assessed"
        });
    }
}
