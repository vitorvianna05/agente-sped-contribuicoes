using System.Security.Claims;
using Api.RAG.Endpoints.Contracts;
using Api.RAG.Rag;

namespace Api.RAG.Tools;

internal sealed class BuscarConhecimentoTool(ConhecimentoService service)
{
    public Task<KnowledgeResult> ExecuteAsync(string consulta, string tipoDocumento, string versaoLayout, ClaimsPrincipal principal,
        string correlation, CancellationToken token) => service.SearchAsync(consulta, tipoDocumento, versaoLayout, principal, correlation, token);
}
