using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Agente.Contracts;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Consumption;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Mcp;
using Api.Agente.Mcp.Contracts;
using Api.Agente.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.Analise;

internal sealed class AnaliseSpedService(ISpedParser parser, SpedValidator validator, IConhecimentoClient knowledge,
    MeasuredParecerLlm llm, IAnalysisLedger ledger, IOptions<AgentDataOptions> configured)
{
    public async Task<AnalysisResult> AnalyzeAsync(Stream file, ExecutionIdentity identity, CancellationToken token)
    {
        await ledger.StartAsync(identity, token); // Sem auditoria aprovada não analisar nem chamar dependências faturáveis.
        ValidationResult validation = new("failed", null, null, null, null, null, [], ["ANALYSIS_NOT_EXECUTED"]);
        KnowledgeEvidence[] evidences = [];
        var knowledgeCalls = new List<KnowledgeTrace>();
        AnalysisResult result;
        try
        {
            var document = await parser.ReadAsync(file, token);
            validation = validator.Validate(document);
            if (validation.State is "failed" or "unsupported")
            {
                result = Result(validation.State, null, validation.State == "unsupported" ? "LAYOUT_NOT_SUPPORTED" : "STRUCTURAL_FAILURE");
            }
            else
            {
                // Contexto montado pelo sistema, sem nomes/CNPJ/arquivo completo ou instruções do upload.
                var query = KnowledgeQuery.Build(validation);
                var retrieved = await CallKnowledge("BuscarConhecimento", () => knowledge.SearchAsync(query, validation.DocumentType!, validation.KnowledgeLayoutVersion!, identity.CorrelationId, token));
                if (retrieved.Status is "invalid_context" or "reference_unavailable") { throw new AnalysisException("KNOWLEDGE_CONTEXT_INVALID"); }
                if (retrieved.Status == "technical_failure") { throw new AnalysisException("KNOWLEDGE_REMOTE_FAILURE"); }
                if (retrieved.Evidences.Length == 0)
                {
                    result = Result("partial", null, "NO_ELIGIBLE_EVIDENCE");
                }
                else
                {
                    evidences = retrieved.Evidences;
                    if (evidences.Select(evidence => evidence.ReferenceId).Distinct().Count() != evidences.Length)
                    { throw new AnalysisException("KNOWLEDGE_REFERENCE_INVALID"); }
                    foreach (var evidence in evidences)
                    {
                        if (evidence.Applicability.DocumentType != validation.DocumentType || evidence.Applicability.LayoutVersion != validation.KnowledgeLayoutVersion || evidence.ReferenceState != "active")
                        { throw new AnalysisException("KNOWLEDGE_CONTEXT_INVALID"); }
                        var resolved = await CallKnowledge("ResolverReferencia", () => knowledge.ResolveAsync(evidence.ReferenceId, identity.CorrelationId, token));
                        var exact = resolved.Evidences.SingleOrDefault();
                        if (resolved.Status != "evidence_found" || exact is null || exact.ReferenceState != "active" ||
                            !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(exact with { Score = null }, AnalysisJson.Options),
                                JsonSerializer.SerializeToNode(evidence with { Score = null }, AnalysisJson.Options)))
                        { throw new AnalysisException("KNOWLEDGE_REFERENCE_INVALID"); }
                    }
                    var draft = await llm.GenerateAsync(identity, new ParecerContext(validation, evidences), token);
                    result = Result("partial", draft, null); // Apenas regras estruturais; cobertura fiscal integral permanece pendente.
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.TimeoutSeconds));
            await ledger.CompleteAsync(identity, "cancelled", "OPERATION_CANCELLED", recording.Token);
            throw;
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        {
            using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.TimeoutSeconds));
            await ledger.CompleteAsync(identity, "failed", "REQUEST_TOO_LARGE", recording.Token);
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var code = exception switch
            {
                AnalysisException known => known.Code,
                KnowledgeClientException known => known.Code,
                SpedReadException known => known.Code,
                _ => "DEPENDENCY_UNAVAILABLE"
            };
            result = Result("failed", null, code);
        }
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.TimeoutSeconds));
        await ledger.CompleteAsync(identity, result.Status, result.ErrorCode, completion.Token);
        return result;

        AnalysisResult Result(string state, ParecerDraft? draft, string? code)
        {
            var output = new AnalysisResult("1.0", identity.ExecutionId, identity.CorrelationId, state, validation,
                state == "failed" ? [] : evidences, draft,
                ["FISCAL_SUFFICIENCY_NOT_ASSESSED", .. code is null ? Array.Empty<string>() : [code]],
                validation.ChecksNotPerformed, code, true, knowledgeCalls.ToArray());
            output.Validate();
            return output;
        }

        async Task<KnowledgeResult> CallKnowledge(string operation, Func<Task<KnowledgeResult>> invoke)
        {
            KnowledgeResult response;
            try { response = await invoke(); }
            catch (KnowledgeClientException exception) when (exception.RemoteCorrelationId is not null)
            {
                await Record(new KnowledgeTrace(operation, exception.RemoteCorrelationId, "technical_failure", []));
                throw;
            }
            await Record(new KnowledgeTrace(operation, response.CorrelationId, response.Status, response.Evidences.Select(evidence => evidence.ReferenceId).ToArray()));
            token.ThrowIfCancellationRequested();
            return response;
        }

        async Task Record(KnowledgeTrace trace)
        {
            using var recording = new CancellationTokenSource(TimeSpan.FromSeconds(configured.Value.TimeoutSeconds));
            await ledger.RecordKnowledgeAsync(identity, trace, recording.Token);
            knowledgeCalls.Add(trace);
        }
    }
}
