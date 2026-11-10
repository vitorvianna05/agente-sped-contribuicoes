# Contrato REST administrativo v1

Api.RAG, HTTPS. Ingestão em `/api/v1/ingest`; acompanhamento sob `/admin/v1`. Uso administrativo
exclusivo: o conteúdo é consultado somente via MCP ([mcp.md](mcp.md)). Estados e invariantes em
[data-model.md](../data-model.md). Decisão vigente: [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md).
Nomes/campos são contratos da aplicação, não layouts ou schemas fiscais.

## Autenticação e convenções

Bearer válido destinado à Api.RAG: assinatura, emissor, audience, validade e permissões.
A identidade vem do token; o corpo não atribui ator ou organização. Permissões administrativas
exigem atribuição central explícita (`Authentication:CentralPermissionAssignments`); administrador
organizacional não herda poder central. `knowledge.read` habilita apenas as tools MCP.
Em Development com `Authentication:DisabledForDevelopment=true`, conexões loopback recebem a
identidade `development-local` com todas as permissões ([ADR 0008](../adrs/0008-configuracao-local-modelos.md)).

Não há `Idempotency-Key`, `ETag` nem `If-Match`. A repetição segura é garantida pela identidade do
documento (seção “Reenvio”). Datas ISO 8601 UTC, IDs UUID, versões oficiais strings opacas e hashes
calculados pelo servidor. Respostas têm `contractVersion: "1.0"` e `correlationId`. `Location`
aponta à operação em caso de 202 e não prova publicação.

## Operações

| Método e caminho | Permissão central | Entrada | Sucesso |
| --- | --- | --- | --- |
| POST `/api/v1/ingest` | `knowledge.ingest` | multipart: `arquivo` PDF e `metadata` JSON (abaixo) | 202 com operação e `location`; 200 quando o reenvio devolve operação já terminal. |
| GET `/admin/v1/operations/{operationId}` | `knowledge.audit` | UUID | 200 com a operação; 404 se não existe. |
| GET `/admin/v1/document-versions/{documentVersionId}` | `knowledge.audit` | UUID | 200 com metadados, `publicationState`, `activeGenerationId`; nunca PDF, texto ou vetor. |

Removidos em 2026-11-10 (ADR 0009), respondem 404: `POST /admin/v1/document-versions/{id}/approve`,
`/reprocess`, `/withdraw`, `GET /admin/v1/document-versions/{id}/retained-artifacts` e
`POST /admin/v1/retained-artifacts/{id}/expurge`.

Em Development, o Swagger em `/swagger` (documento `/openapi/v1.json`) descreve essas operações
([ADR 0010](../adrs/0010-ambiente-local-openai-swagger.md)).

## Metadados da ingestão

`metadata` (até 64 KiB) requer, sem campos extras, repetidos ou nulos:

- `contractVersion`: literal `1.0`.
- `issuer`, `officialIdentifier`, `title`: strings não vazias (até 1000 caracteres).
- `kind`: `manual` ou `guide`.
- `originUri`: URI HTTPS da origem oficial (até 4000), registrada e não baixada automaticamente.
- `officialVersion`: string não vazia, sem inferência por data ou nome de arquivo.
- `applicabilities`: 1 a 100 itens `{documentType, layoutVersion, officialBasis}`, sem par
  tipo/layout repetido. A busca filtra exatamente por esses valores.
- `profileId`: UUID de um perfil em `Processing:Profiles`.

Upload admite exatamente um arquivo `arquivo` (`application/pdf`, assinatura `%PDF-`) e um campo
`metadata`; o nome do arquivo não define versão. Limites `Processing:MaxFileBytes` e
`Processing:MaxPages`. Somente PDF com texto extraível; OCR e outros formatos são recusados.
Validação possível antes da aceitação gera erro síncrono; falha posterior fica na operação.

## Reenvio e idempotência natural

A identidade é `issuer` + `officialIdentifier` + `officialVersion` + `profileId`:

- geração concluída ou em andamento, com trechos → devolve a operação existente sem reprocessar
  (200 se terminal, 202 caso contrário); se essa geração estiver concluída e a versão não publicada,
  ela é publicada;
- somente gerações `failed`, ou texto pronto sem trechos → nova operação e nova geração;
- mesma versão com bytes diferentes → 409 `VERSION_CONTENT_CONFLICT`;
- `title`/`kind`/`originUri` diferentes para a mesma fonte, ou demais metadados diferentes para a
  mesma versão → 409 `SOURCE_METADATA_CONFLICT`;
- `profileId` com definição diferente da persistida → 409 `PROFILE_INCOMPATIBLE`.

Envios concorrentes do mesmo documento são serializados e convergem para a mesma operação; nenhuma
operação órfã fica em `queued`. Para refazer um documento, reenviá-lo; não apagar trechos à mão.

## Acompanhamento e publicação

POST retorna `{contractVersion,correlationId,operation,location}`; os GET retornam
`{contractVersion,correlationId,operation}` e `{contractVersion,correlationId,document}`.
`operation`: operationId, type, state, documentVersionId, generationId, createdAt, startedAt,
finishedAt, resultCode, preparationState, revision. `document`: documentVersionId, metadata,
originalContentHash, publicationState, activeGenerationId, revision, createdAt. `revision` é
informativo (não há precondição).

Fluxo: `preparationState` queued → processing → ready (texto extraído e segmentado; `resultCode`
`EMBEDDINGS_PENDING`) → etapa vetorial (`EMBEDDINGS_PROCESSING`) → `state` succeeded/failed.
Em `succeeded`, a versão passa a `published` automaticamente e a geração vira a ativa; publicar uma
versão retira (`withdrawn`) as outras versões publicadas da mesma fonte. Sem etapa vetorial ativa
(sem chave OpenAI), a operação para em ready/EMBEDDINGS_PENDING e a versão segue `unpublished`.

Falhas monitoráveis: `PDF_TEXT_UNAVAILABLE`, `PDF_PAGE_LIMIT` (sem trechos parciais),
`TECHNICAL_FAILURE`, `OPERATION_TIMEOUT` (geração inteira deve caber em
`Processing:TimeoutSeconds`), `PROCESSING_ATTEMPTS_EXHAUSTED` (limite
`Processing:MaxPreparationAttempts`), `DEPENDENCY_UNAVAILABLE` (provedor de embeddings após
tentativas limitadas). Perfis aceitos: ver [data-model.md](../data-model.md) e ADR 0005.

## Falhas versionadas

Problem Details com `type`, `title`, `status`, `detail` sanitizado e extensões `code`,
`correlationId`, `contractVersion`; sem stack, segredo, bytes ou texto fiscal.

| HTTP | Códigos da aplicação |
| --- | --- |
| 400 | INVALID_METADATA, INVALID_ARGUMENT |
| 401 / 403 | AUTHENTICATION_REQUIRED / PERMISSION_DENIED |
| 404 | RESOURCE_NOT_FOUND (inclui rotas removidas) |
| 409 | VERSION_CONTENT_CONFLICT, SOURCE_METADATA_CONFLICT, PROFILE_INCOMPATIBLE, INVALID_STATE, RETENTION_NOT_CONFIGURED |
| 413 / 415 / 422 | FILE_TOO_LARGE / UNSUPPORTED_FORMAT / PDF_TEXT_UNAVAILABLE, PDF_PAGE_LIMIT |
| 429 | ADMISSION_LIMIT |
| 503 / 504 | DEPENDENCY_UNAVAILABLE / OPERATION_TIMEOUT |

Limites, timeout, tentativas e lease são configuração finita obrigatória validada no startup.
Cancelamento é propagado, mas resposta cancelada não prova ausência de efeito; consultar a operação.

## Testes do contrato

Payload e metadados inválidos, token/permissão, convergência de envios concorrentes sem operação
órfã, conflito de bytes e de metadados, publicação automática, nova versão substituindo a anterior,
reenvio após falha e após trechos apagados, publicação de geração concluída não publicada e 404 das
rotas removidas (`IngestionContractTests`, `CompleteRagJourneyTests`, `IngestionJourneyTests`).
