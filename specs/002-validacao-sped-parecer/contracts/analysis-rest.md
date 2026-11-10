# Análise REST técnica e parecer — contrato 1.0

Data: 2026-11-10. Implementado, condicionado à configuração e aos gates. Não é A2A.

## POST /api/v1/analises

Exige analysis.execute e subject do transporte. Corpo: bytes SPED Latin-1, como
application/octet-stream ou text/plain; charset=iso-8859-1. Sem JSON, multipart, caminho
local ou identidade no payload. Reutiliza limites/cancelamento HTTP e do parser.
executionId e correlationId são gerados pelo sistema; correlationId também em X-Correlation-Id.
Resposta no-store. Não persiste o arquivo nem o texto do resultado.

AnalysisResult tem contractVersion, executionId, correlationId, status, validation,
evidences, parecer (nullable), limitations, checksNotPerformed, errorCode (nullable),
humanReviewRequired e knowledgeCalls. Cada knowledgeCalls contém operation,
remoteCorrelationId, status e referenceIds para vincular os registros da RAG à execução local,
mesmo se o servidor gerar outra correlação. Respostas de erro remoto válidas também mantêm
esse vínculo; falha sem resposta/contrato válido não inventa ID remoto. Validation contém state, declaredLayoutCode, periodFrom/Through,
documentType/knowledgeLayoutVersion e checks (ruleId/state/lineNumber/sourceReference).
Evidences preserva os DTOs do MCP 1.0: IDs, fonte, versão, texto, aplicabilidade e localização.

| Condição | status | parecer / comportamento |
| --- | --- | --- |
| Leitura/envelope/data inválida | failed | null; erro estrutural explícito; não consulta RAG/LLM. |
| COD_VER/período sem perfil único ou fora de 2020–2026 | unsupported | null; LAYOUT_NOT_SUPPORTED; não presume versão/tipo. |
| Nenhuma evidência elegível | partial | null; NO_ELIGIBLE_EVIDENCE; não chama LLM. |
| MCP/contrato/resolução/LLM inválido ou indisponível | failed | null; código técnico; validações já feitas continuam separadas. |
| Evidências resolvidas e rascunho JSON validado | partial | rascunho; regras fiscais/semântica integral permanecem pendentes. |

Esses resultados são HTTP 200 com estado de negócio/técnico explícito. completed é reservado
no contrato; o fluxo atual nunca o emite. Nenhum resultado significa conformidade tributária.
O retorno contém FISCAL_SUFFICIENCY_NOT_ASSESSED e exige revisão humana.

HTTP 401/403: transporte/permissão; 415: mídia/charset; 413: limite HTTP, inclusive chunked;
503: auditoria/configuração/dependência que impede iniciar ou concluir com rastreabilidade;
504: deadline. ProblemDetails inclui metadados do sistema, sem mensagens internas/segredos.
Limite/erro do parser depois de iniciada a análise pode ser um resultado failed com seu
código SPED; o endpoint de leitura preserva seu próprio contrato de erros.

## JSON do modelo

Schema versionado: src/Api.Agente/Llm/parecer.schema.json; prompt parecer-v1.txt.
Campos obrigatórios: explanation, interpretedInconsistencies (códigos exatos dos checks
failed), referenceIds (não vazios, únicos e presentes no contexto), limitations (preserva
todas as verificações não realizadas), humanReviewRequired=true. Não aceita campos extras,
operacionais, duplicados, nulos indevidos ou referência inventada. Explicação é um rascunho
do modelo; qualidade substantiva/injeção adversarial real ainda exige corpus/rubrica AG014.

Antes da geração, cada evidência é resolvida por ResolverReferencia e comparada integralmente
(exceto score, próprio do ranking), inclusive estado active, proveniência/localização/texto.
Uma divergência ou retirada impede geração. Similaridade não é validação fiscal.

## GET /api/v1/analises/{executionId}/consumo

Mesma autorização; consulta somente execução do subject + organization_id autenticados.
Outro proprietário, organização ou execução expirada/inexistente recebe 404, sem revelar
existência. Retorna contractVersion, executionId, correlationId da consulta, attempts,
attemptCount, unknownCostAttempts e byModel. Tentativas incluem estado, modelo/prompt,
início/fim/duração, tokens e campos separados de custo/moeda estimados/informados.
byModel soma apenas tokens conhecidos, expõe unknownUsageAttempts e mantém total conhecido
nulo se nenhum contador foi informado. Não soma moedas nem afirma custo zero. Não inclui
arquivo, prompt, parecer ou credencial. Sem tentativas, retorna lista vazia.

## Tentativas e dados

Timeout, MaxAttempts, delay e limites do contexto/saída são externos e finitos. Retry
explícito somente para timeout, saída JSON inválida e HTTP 408/429/5xx; recusa/incompleto
não são repetidos automaticamente. SDK não repete internamente. Cancelamento registra
estado com token independente; encerramento abrupto pode manter pending, nunca sucesso falso.
Preço ou faturamento não informado permanece NULL; cálculo tarifário é incremento dependente
de tabela/versionamento aprovados. Não agrega o consumo de embeddings da Api.RAG.

PolicyVersion/ApprovalReference e prazos não têm defaults de aprovação. Arquivo/resultados
são transitórios. Schema agente e migração/expurgo são exclusivamente da Api.Agente.
Detalhes: [ADR 0003](../adrs/0003-fluxo-parecer-auditoria.md), [quickstart](../quickstart.md).
