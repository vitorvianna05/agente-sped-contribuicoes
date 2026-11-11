# Contratos iniciais da Api.Agente — v1

Data: 2026-11-10. Registro do incremento inicial de diagnóstico técnico. O contrato de leitura
foi acrescentado em [parser-sped.md](parser-sped.md) e o de análise em [analysis-rest.md](analysis-rest.md).
Este diagnóstico não acrescenta regras fiscais ou declara layout suportado.

## HTTP disponível

| Rota | Autorização | Resultado |
| --- | --- | --- |
| GET /health/live | Sem JWT; sujeito a HTTPS/hosts | status=alive e analysisAvailable=false. Não comprova dependências. |
| GET /admin/v1/knowledge/readiness | analysis.diagnose e principal administrativo configurado; ou modo local autorizado | contractVersion, executionId/correlationId do sistema, transportReady, nomes das duas tools, analysisAvailable=false, fiscalReadiness=not_assessed. |

Nenhuma rota de diagnóstico retorna texto, vetor, fonte ou parecer. HTTP 401/403 descrevem
identidade/permissão local; 413 limite de entrada; 503 falha de dependência/contrato remoto;
504 prazo expirado. ProblemDetails contém code, executionId, correlationId e contractVersion, sem mensagem
interna do transporte. correlationId também está em X-Correlation-Id e não vem do cabeçalho
fornecido pelo chamador. Valores de timeout/tamanho vêm de configuração validada.

## Evolução dos contratos internos

Na fundação foram ensaiados protótipos internos AnalysisRequest e AnalysisEnvelope, sem rota
funcional publicada. O incremento de análise adotou corpo bruto e AnalysisResult, removendo
os protótipos sem uso. Identidade e identificação de layout pertencem ao sistema/arquivo;
o POST não aceita metadata JSON nem multipart. A2A possui contrato separado na Feature 003.

AnalysisResult separa executionId/correlationId/status, validações, inconsistências,
parecer, limitações e verificações não realizadas. Estados: completed, partial, failed,
unsupported; completed não é conformidade tributária. Failed/unsupported não carregam parecer;
completed não pode ocultar checksNotPerformed. O fluxo atual retorna partial/failed/unsupported,
com revisão humana; formato detalhado das regras fiscais depende de AG001/AG006.

ParecerDraft contém explanation, interpretedInconsistencies, referenceIds, limitations e
humanReviewRequired; rejeita metadados operacionais extras. Confronto de citações e preservação
dos resultados foram implementados em AG009/AG010; qualidade do texto exige avaliação AG014.

## MCP consumido

IConhecimentoClient.SearchAsync recebe query, documentType, layoutVersion e correlação do
sistema; falta de contexto é erro antes da rede. Só chama BuscarConhecimento com os três
argumentos do contrato. ResolveAsync só chama ResolverReferencia e confere o UUID retornado.
MCP não recebe principal ou configuração no corpo. Configuração fixa destino e bearer de
serviço; não há query funcional REST nem credencial de banco RAG.
A query é montada por `KnowledgeQuery.Build` a partir das regras executadas, no vocabulário do
guia (títulos de registros/seções, leiaute e período); códigos internos de regra e conteúdo do
arquivo não são enviados (feature 001, ADR 0011).

ConhecimentoClientException possui códigos sanitizados: KNOWLEDGE_CONTEXT_REQUIRED,
KNOWLEDGE_REFERENCE_INVALID, CORRELATION_INVALID, KNOWLEDGE_SCHEMA_INCOMPATIBLE,
KNOWLEDGE_RESPONSE_INVALID, KNOWLEDGE_REMOTE_FAILURE, KNOWLEDGE_ACCESS_DENIED,
KNOWLEDGE_RESPONSE_TOO_LARGE, KNOWLEDGE_TIMEOUT e KNOWLEDGE_UNAVAILABLE.
Cancelamento do chamador propaga OperationCanceledException; não vira busca vazia.

Contrato MCP independente: src/Api.Agente/Mcp/Contracts/knowledge-v1.schema.json.
Proveniência/compatibilidade: [ADR 0001](../adrs/0001-fundacao-cliente-mcp.md).
