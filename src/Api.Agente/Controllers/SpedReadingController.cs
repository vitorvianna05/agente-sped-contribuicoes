using Api.Agente.Infrastructure.OpenApi;
using Api.Agente.Validacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Agente.Controllers;

/// <summary>Leitura estrutural de SPED sem persistência, consulta MCP ou parecer fiscal.</summary>
[ApiController, Route("api/v1/sped"), Authorize(Policy = "analysis.execute")]
public sealed class SpedReadingController : ControllerBase
{
    private static readonly string[] Charsets = ["iso-8859-1", "latin1", "latin-1"];
    private static readonly string[] PendingChecks = ["LAYOUT_COMPATIBILITY_NOT_ASSESSED", "RECORD_FIELD_SCHEMAS_NOT_VALIDATED",
        "BLOCK_HIERARCHY_NOT_VALIDATED", "FISCAL_RULES_NOT_EXECUTED"];

    /// <summary>Lê arquivo texto enviado diretamente no corpo, preservando campos e linhas.</summary>
    /// <param name="token">Cancelamento da requisição.</param>
    /// <returns>Registros, ocorrências estruturais e limitações explícitas.</returns>
    [HttpPost("leitura"), Consumes("text/plain", "application/octet-stream"), OpenApiSpedUpload]
    public async Task<IActionResult> Read(CancellationToken token)
    {
        var charset = Request.GetTypedHeaders().ContentType?.Charset.Value;
        if (!string.IsNullOrWhiteSpace(charset) && !Charsets.Contains(charset.Trim('"'), StringComparer.OrdinalIgnoreCase))
        {
            throw new SpedReadException("SPED_ENCODING_NOT_SUPPORTED");
        }
        var parser = HttpContext.RequestServices.GetRequiredService<ISpedParser>();
        var document = await parser.ReadAsync(Request.Body, token);
        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            contractVersion = "1.0",
            executionId = (Guid)HttpContext.Items["ExecutionId"]!,
            correlationId = HttpContext.TraceIdentifier,
            status = document.Issues.Length == 0 ? "partial" : "failed",
            readingStatus = document.Issues.Length == 0 ? "completed" : "invalid",
            fiscalValidationStatus = "not_performed",
            document,
            checksNotPerformed = PendingChecks
        });
    }
}
