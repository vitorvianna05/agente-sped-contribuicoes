using System.Text;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Endpoints.Middleware;
using Api.RAG.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.RAG.Tests.Contracts;

/// <summary>Erros públicos mantêm correlação, versão e ausência de conteúdo sensível.</summary>
public sealed class ProblemDetailsTests
{
    /// <summary>Mensagem e stack da exceção não fazem parte do contrato de erro.</summary>
    [Fact]
    [Trait("Category", "Contract")]
    public async Task UnexpectedFailureIsSanitized()
    {
        await using var host = new RagTestHost();
        using var client = host.CreateClient();
        var context = new DefaultHttpContext { RequestServices = host.Services, TraceIdentifier = "system-only" };
        await using var body = new MemoryStream();
        context.Response.Body = body;
        var handler = new ProblemDetailsHandler(_ => throw new InvalidOperationException("secret-token-never-log"),
            NullLogger<ProblemDetailsHandler>.Instance);
        await handler.InvokeAsync(context);
        var json = Encoding.UTF8.GetString(body.ToArray());
        Assert.Equal(503, context.Response.StatusCode);
        Assert.Contains("system-only", json, StringComparison.Ordinal);
        Assert.Contains("contractVersion", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Erros de autorização e precondição mantêm seus códigos HTTP distintos.</summary>
    [Theory]
    [InlineData("PERMISSION_DENIED", 403)]
    [InlineData("PRECONDITION_REQUIRED", 428)]
    [InlineData("REVISION_MISMATCH", 412)]
    [InlineData("IDEMPOTENCY_CONFLICT", 409)]
    [Trait("Category", "Contract")]
    public void DomainErrorsKeepTheirHttpMeaning(string code, int status)
    {
        Assert.Equal(status, ProblemDetailsHandler.CodeStatus(new KnowledgeException(code).Code));
    }
}
