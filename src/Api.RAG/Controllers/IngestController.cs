using System.Text.Json;
using Api.RAG.Application.Knowledge.Consulta;
using Api.RAG.Application.Knowledge.Ingestao;
using Api.RAG.Domain.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.RAG.Controllers;

/// <summary>Administração da ingestão; conteúdo funcional permanece exclusivo do MCP.</summary>
[Route("api/v1/ingest")]
[Authorize(Policy = "knowledge.ingest")]
internal sealed class IngestController(IngerirFonte ingestion) : ControllerBase
{
    /// <summary>Recebe PDF e metadados para processamento durável, sem publicação automática.</summary>
    [HttpPost]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType || !Request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw new KnowledgeException("UNSUPPORTED_FORMAT");
        }
        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            throw new KnowledgeException("INVALID_ARGUMENT");
        }
        if (form.Files.Count != 1 || form.Files[0].Name != "arquivo" || form.Count != 1 ||
            !form.TryGetValue("metadata", out var values) || values.Count != 1 || values[0] is not { Length: > 0 and <= 65536 } json ||
            !Request.Headers.TryGetValue("Idempotency-Key", out var keys) || keys.Count != 1)
        {
            throw new KnowledgeException("INVALID_METADATA");
        }
        IngestionMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<IngestionMetadata>(json, IngestionMetadata.JsonOptions)
                ?? throw new KnowledgeException("INVALID_METADATA");
        }
        catch (JsonException)
        {
            throw new KnowledgeException("INVALID_METADATA");
        }
        var file = form.Files[0];
        await using var content = file.OpenReadStream();
        var result = await ingestion.ExecuteAsync(metadata, content,
            file.ContentType, keys.ToString(), User, HttpContext.TraceIdentifier, cancellationToken);
        Response.Headers.ETag = result.Revision;
        var location = $"/admin/v1/operations/{result.OperationId}";
        var body = new { contractVersion = "1.0", correlationId = HttpContext.TraceIdentifier, operation = result, location };
        return result.State is "succeeded" or "failed" or "cancelled" ? Ok(body) : Accepted(location, body);
    }
}

/// <summary>Consulta administrativa de metadados de ingestão.</summary>
[Route("admin/v1")]
[Authorize(Policy = "knowledge.audit")]
internal sealed class IngestionStatusController(ConsultarIngestao queries) : ControllerBase
{
    /// <summary>Consulta o estado persistido da operação.</summary>
    [HttpGet("operations/{id:guid}")]
    public async Task<IActionResult> Operation(Guid id, CancellationToken cancellationToken)
    {
        var result = await queries.OperationAsync(id, User, cancellationToken);
        Response.Headers.ETag = result.Revision;
        return Ok(new { contractVersion = "1.0", correlationId = HttpContext.TraceIdentifier, operation = result });
    }

    /// <summary>Consulta metadados da versão, sem expor PDF, trechos ou vetores.</summary>
    [HttpGet("document-versions/{id:guid}")]
    public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
    {
        var result = await queries.DocumentAsync(id, User, cancellationToken);
        Response.Headers.ETag = result.Revision;
        return Ok(new { contractVersion = "1.0", correlationId = HttpContext.TraceIdentifier, document = result });
    }
}
