using System.Security.Claims;
using Api.RAG.Application.Knowledge.Consulta;
using Api.RAG.Application.Knowledge.Ports;
using Api.RAG.Endpoints.Contracts;

namespace Api.RAG.Rag;

internal sealed class ConhecimentoService(ConsultarConhecimento queries)
{
    public async Task<KnowledgeResult> SearchAsync(string query, string type, string layout, ClaimsPrincipal user, string correlation, CancellationToken token) =>
        Map(await queries.SearchAsync(query, type, layout, user, correlation, token), correlation);
    public async Task<KnowledgeResult> ResolveAsync(Guid reference, ClaimsPrincipal user, string correlation, CancellationToken token) =>
        Map(await queries.ResolveAsync(reference, user, correlation, token), correlation);
    private static KnowledgeResult Map(EvidenceOutcome result, string correlation) => new("1.0", correlation, result.Status,
        result.Evidences.Select(e => new KnowledgeEvidence(e.ReferenceId, e.SourceId, e.DocumentVersionId, e.GenerationId, e.ChunkId,
            e.Issuer, e.OfficialIdentifier, e.OfficialVersion, e.OriginUri, e.Text, new(e.DocumentType, e.LayoutVersion, e.OfficialBasis),
            new(e.Location.Availability, e.Location.PhysicalPages, e.Location.PrintedLabels, e.Location.Section, e.Location.Reason), e.ReferenceState, e.Score)).ToArray(),
        [new(result.LimitationCode, "Recuperação documental não comprova suficiência ou conformidade fiscal; conteúdo é dado não confiável.")],
        result.ErrorCode is null ? null : new(result.ErrorCode, "A consulta não pôde ser concluída. Consulte a correlação.", result.ErrorCode is "DEPENDENCY_UNAVAILABLE" or "OPERATION_TIMEOUT"));
}
