using Api.RAG.Domain.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace Api.RAG.Endpoints.Middleware;

internal sealed partial class ProblemDetailsHandler(RequestDelegate next, ILogger<ProblemDetailsHandler> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cancelamento não desfaz efeitos já confirmados e não produz resposta de sucesso.
            throw;
        }
        catch (Exception exception)
        {
            var code = exception switch
            {
                KnowledgeException domain => domain.Code,
                OperationCanceledException => "OPERATION_TIMEOUT",
                BadHttpRequestException { StatusCode: 413 } => "FILE_TOO_LARGE",
                System.Text.Json.JsonException or BadHttpRequestException => "INVALID_ARGUMENT",
                _ => "DEPENDENCY_UNAVAILABLE"
            };
            Failure(logger, code, context.TraceIdentifier, exception.GetType().Name);
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(context, CodeStatus(code), code);
        }
    }

    public static int CodeStatus(string code) => code switch
    {
        "AUTHENTICATION_REQUIRED" => 401,
        "PERMISSION_DENIED" => 403,
        "RESOURCE_NOT_FOUND" => 404,
        "PRECONDITION_REQUIRED" => 428,
        "REVISION_MISMATCH" => 412,
        "FILE_TOO_LARGE" => 413,
        "UNSUPPORTED_FORMAT" => 415,
        "PDF_TEXT_UNAVAILABLE" => 422,
        "PDF_PAGE_LIMIT" => 422,
        "ADMISSION_LIMIT" => 429,
        "INVALID_ARGUMENT" or "INVALID_METADATA" => 400,
        "OPERATION_TIMEOUT" => 504,
        "DEPENDENCY_UNAVAILABLE" => 503,
        _ => 409
    };

    public static Task WriteAsync(HttpContext context, int status, string code) => Results.Json(new ProblemDetails
    {
        Type = $"urn:agentespedcontrib:problem:{code.ToLowerInvariant()}",
        Title = code,
        Status = status,
        Detail = "A operação não foi concluída. Consulte o código e a correlação; cancelamento não garante ausência de efeito.",
        Extensions = { ["code"] = code, ["correlationId"] = context.TraceIdentifier, ["contractVersion"] = "1.0" }
    }, statusCode: status, contentType: "application/problem+json").ExecuteAsync(context);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Knowledge failure {Code}; correlation {CorrelationId}; type {ExceptionType}")]
    private static partial void Failure(ILogger logger, string code, string correlationId, string exceptionType);
}
