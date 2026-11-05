# Contrato REST administrativo v1

Api.RAG, HTTPS, ingestão em `/api/v1/ingest` e demais operações sob `/admin/v1`. Uso administrativo exclusivo; consumidores funcionais
não consultam conhecimento por REST. Os estados e invariantes estão em [data-model.md](../data-model.md).
Nomes/campos são contratos da aplicação, não layouts ou schemas fiscais.

## Autenticação e convenções

Bearer válido destinado à Api.RAG. Validar assinatura, emissor, audience, validade e permissões.
A identidade vem do token; corpo não atribui ator ou organização para obter privilégio.
Permissões administrativas exigem atribuição central explícita. Administrador organizacional
não herda poder central. `knowledge.read` habilita apenas as tools funcionais MCP.

Pedidos mutáveis exigem `Idempotency-Key`, escopada por principal e operação; replay do mesmo
pedido retorna operação/efeito original e não reinicia retenção. Mesmo identificador com
payload diferente retorna 409 `IDEMPOTENCY_CONFLICT`. Guardar digest, não chave secreta em log.
Estado existente é protegido por `ETag`/`If-Match` de revisão; cabeçalho ausente: 428;
revisão diferente: 412. Replays idempotentes confirmados precedem a validação da revisão antiga.
Nenhuma comparação em memória substitui constraint/transação no banco.

Datas ISO 8601 UTC, IDs UUID, versões oficiais strings opacas e hashes calculados pelo servidor.
Bodies JSON têm `contractVersion: "1.0"`, campos documentados e rejeitam propriedades desconhecidas.
Respostas têm `contractVersion`, `correlationId` gerado/validado pelo sistema, estado e IDs.
`Location` aponta à operação em caso de 202, não prova publicação. 202/200/201 obedecem à
existência ou criação do recurso, conforme cada operação abaixo.

## Revisão estrutural — 2026-11-05

IngestController segue o padrão da Api.Conhecimento de referência, com multipart arquivo +
metadata JSON fiscal. A rota planejada anterior /admin/v1/ingestions passa a /api/v1/ingest.
POST /api/v1/ingest e os dois GET de acompanhamento estão implementados no incremento textual.
As demais operações de T033, incluindo approve, continuam pendentes. Demais caminhos da tabela são
relativos a /admin/v1. Mantêm-se 202/acompanhamento, aprovação, JWT e idempotência; não
copiar o campo usuario livre ou a substituição destrutiva da referência.

## Operações

| Método e caminho | Permissão central | Entrada | Sucesso |
| --- | --- | --- | --- |
| POST `/api/v1/ingest` | `knowledge.ingest` | multipart: `arquivo` PDF e `metadata` JSON descrito abaixo; Idempotency-Key | 202 com operationId e acompanhamento; replay pode retornar 200 para resultado já concluído, sem novo processamento. |
| GET `/operations/{operationId}` | `knowledge.audit` | ID válido; acesso administrativo central | 200 com tipo, estado, documentVersionId/generationId quando conhecidos, timestamps, código de falha e ETag; 404 se não existe. |
| GET `/document-versions/{documentVersionId}` | `knowledge.audit` | ID válido | 200 com metadados, publicação, geração ativa e ETag; não retorna busca ou texto funcional. |
| POST `/document-versions/{id}/approve` | `knowledge.publish` | JSON `{contractVersion, generationId}`; If-Match e Idempotency-Key | 200 com estado published, revisão e aprovação; somente geração concluída inicial unpublished, inclusive pelo próprio autor da ingestão. |
| POST `/document-versions/{id}/reprocess` | `knowledge.reprocess` | JSON `{contractVersion, profileId}`; If-Match e Idempotency-Key | 202 com operação e geração candidata; não aceita novo PDF nem muda origem. |
| POST `/document-versions/{id}/withdraw` | `knowledge.withdraw` | JSON `{contractVersion, reason}`; If-Match e Idempotency-Key | 200 com withdrawn, marco imutável e revisão; mantém histórico autorizado. |
| GET `/document-versions/{id}/retained-artifacts` | `knowledge.audit` | ID válido de versão documental; somente administrador central autorizado | 200 com metadados e revisão de cada artefato conforme seção abaixo; lista vazia quando não houver artefatos retidos, 404 para versão inexistente. |
| POST `/retained-artifacts/{id}/expurge` | `knowledge.expurge` | JSON `{contractVersion, reason}`; If-Match e Idempotency-Key | 200 com purged, instante e revisão; exige prazo vencido e ausência de dependência ativa. |

Não oferecer restauração de fonte retirada ou correção dos bytes sob mesma versão nesta entrega.
Perfil candidate pode ser processado para avaliação em ambiente controlado; sua existência
não torna a geração elegível para consulta operacional. Não mudar perfil ativo do corpus por
um reprocessamento isolado incompatível. Sem perfil adotado, operação não pode publicar evidência.

## Metadados da ingestão

`metadata` requer:

- `contractVersion`: literal `1.0`.
- `issuer`, `officialIdentifier`, `title`: strings não vazias, limites externos validados.
- `kind`: `manual` ou `guide`.
- `originUri`: URI HTTPS da origem oficial, registrada e verificável; não baixada automaticamente.
- `officialVersion`: string não vazia, sem inferência por data ou nome de arquivo.
- `applicabilities`: lista não vazia de `{documentType, layoutVersion, officialBasis}`.
  Cada valor deve ter fundamento oficial; o servidor não inventa relação de compatibilidade.
- `profileId`: UUID do perfil versionado, configurado para o ambiente e operação.

PDF: validar limite de bytes, MIME/conteúdo real e condições de extração. Somente manuais/guias
PDF com texto extraível; OCR, schemas e outros formatos são recusados. Validação possível antes
da aceitação produz erro síncrono; falha de extração posterior fica na operação failed.
Aceitar upload não significa que todo o PDF pôde ser extraído ou que a origem foi aprovada.
Hash é dos bytes recebidos; mesmo source+versão e hash diferente retorna conflito, sem criar
revisão interna. Mesmo conteúdo reingerido preserva identidade/proveniência, sem duplicata ativa.

## Acompanhamento e publicação

### Incremento textual implementado — ajustes 1–4

POST retorna `{contractVersion,correlationId,operation,location}`. Os dois GET retornam
`{contractVersion,correlationId,operation}` e `{contractVersion,correlationId,document}`.
ETag forte é enviado também no cabeçalho e em revision. IDs técnicos são UUIDs.
operation contém operationId/type/state/documentVersionId/generationId/createdAt/startedAt/
finishedAt/resultCode/preparationState/revision; document contém documentVersionId/metadata/
originalContentHash/publicationState/activeGenerationId/revision/createdAt. Nenhum GET retorna PDF,
texto, spans ou vetor. Ambos exigem knowledge.audit central, mesmo para o autor do upload.

preparationState: queued, processing, ready, failed. Extração roda em background após a aceitação.
Texto pronto ainda deixa operação queued, resultCode EMBEDDINGS_PENDING e finishedAt null;
a geração completa aguarda embeddings e a versão permanece unpublished. Erro de PDF fica em
failed com código PDF_TEXT_UNAVAILABLE ou PDF_PAGE_LIMIT, sem trechos parciais persistidos.
Replay de operação terminal retorna 200; outra chave para o mesmo conteúdo/perfil retorna
a operação canônica completa (IDs, timestamps e ETag), sem criar outra geração. A chave
adicional é registrada internamente, mas não cria uma cronologia híbrida na resposta.
Mudança de origem/metadados existentes gera 409 SOURCE_METADATA_CONFLICT.
Falha técnica do worker registra failed/TECHNICAL_FAILURE, distinta de PDF_TEXT_UNAVAILABLE.
Tentativas interrompidas ou sem confirmação têm limite externo Processing.MaxPreparationAttempts;
ao esgotar o limite, failed/PROCESSING_ATTEMPTS_EXHAUSTED é persistido junto à auditoria.
Parâmetros e limitação de cancelamento do parser: ADR 0005.

Perfis aceitos neste incremento exigem UUID, ExtractorVersion
`pdfpig-0.1.16-reading-order-v1`, NormalizationVersion/QueryNormalizationVersion
`line-endings-v1`, TokenizerVersion `whitespace-v1`, Segmenter `structural` ou `token-window`,
ChunkTokens positivo e 0 <= OverlapTokens < ChunkTokens; todos externos. Modelo, provider,
dimensões e métrica também ficam no snapshot imutável. Não há parâmetros produtivos aprovados.
Campos inesperados, repetidos, ausentes ou null em metadata são recusados. Metadados têm
até 64 KiB; origem até 4000 caracteres, demais strings até 1000 e até 100 aplicabilidades.
Upload admite um arquivo e um campo metadata; nome do arquivo não define versão ou destino.

Estados de processamento: queued, processing, succeeded, failed, cancelled.
Publicação: unpublished, published, withdrawn, purged; retornada separadamente.
Uma geração inicialmente succeeded continua unpublished até approve explícito. Reprocessamento
de versão publicada mantém geração anterior; sucesso troca a geração integralmente sem nova
aprovação se o estado/revisão permitir. Falha ou retirada concorrente não reativa nem troca texto.
O status da operação deve indicar falha de ativação/concorrência quando aplicável, sem
apresentar mera extração concluída como publicação concluída.

## Descoberta de artefatos retidos

`GET /admin/v1/document-versions/{id}/retained-artifacts` retorna objeto JSON com
`contractVersion: "1.0"`, `correlationId`, `documentVersionId` e `items` (array, inclusive vazio).
Schema de resposta em [retained-artifacts.schema.json](retained-artifacts.schema.json).
Cada item DEVE conter os campos abaixo; valores desconhecidos permanecem explicitamente nulos:

| Campo | Tipo e significado |
| --- | --- |
| `retainedArtifactId` | UUID do recurso a usar em POST `/retained-artifacts/{id}/expurge`. |
| `category` | String não vazia da categoria de retenção registrada. |
| `state` | `retained` ou `purged`, sem representar publicação ou elegibilidade por esse campo. |
| `eventType` | `withdrawn` ou `superseded`, conforme evento persistido. |
| `eventAt` | Instante UTC ISO 8601 do evento, sem reiniciar por consulta/replay. |
| `policyVersion` | String ou null se não houver política aplicável configurada. |
| `eligibleForExpurgeAt` | Instante UTC ou null sem prazo calculável/aprovado. |
| `eligibleForExpurge` | Booleano de elegibilidade de estado/prazo/dependências naquele snapshot; false sem política. Não inclui concessão de permissão de exclusão. |
| `impediments` | Array de códigos não vazios; vazio se elegível. Inclui RETENTION_NOT_CONFIGURED, RETENTION_NOT_EXPIRED, ACTIVE_DEPENDENCY ou ALREADY_PURGED quando aplicáveis. |
| `revision` | Token opaco de revisão do artefato, já no formato de ETag forte entre aspas, utilizável literalmente no cabeçalho If-Match do expurgo. Não é a revisão da versão documental nem da lista. |

O servidor emite revision entre aspas com caracteres ASCII alfanuméricos, ponto, sublinhado,
dois-pontos ou hífen; valores fracos com prefixo W/ não atendem à precondição deste contrato.

A resposta NÃO DEVE incluir PDF, texto de trecho, embedding ou endereço direto do armazenamento.
O token revision DEVE representar a revisão e os metadados de estado/política/dependências
usados na projeção, inclusive a elegibilidade calculada. Mudança desses metadados invalida
o token; o servidor o recalcula no snapshot do comando, em vez de confiar no valor enviado.
Consulta é somente administrativa e usa snapshot consistente para seus metadados; não substitui
resolução histórica MCP. Permissão de auditoria não concede knowledge.expurge. Em especial,
`eligibleForExpurge: true` não autoriza excluir. Após mudança concorrente, expurgo com revision
antiga retorna 412 REVISION_MISMATCH; o cliente deve reler os metadados e reenviar somente se
ainda autorizado. Se política/prazo/dependências impedir o efeito, aplicar os códigos 409
correspondentes, sem apagar. Artefato já purged não é elegível; tombstone segue sua própria
retenção. Os identificadores não são reciclados.

## Expurgo e retenção

Revalidar principal central autorizado, configuração versionada, categoria, marco de retirada
ou substituição, vencimento, revisão e dependências na transação do efeito. Ausência de prazo,
prazo não vencido e conteúdo ativo impedem a exclusão. Vencimento não dispara job de exclusão.
O expurgo é dirigido a artefato; excluir geração antiga não autoriza apagar o PDF usado por
outra geração. Retry não reinicia retenção ou duplica efeito/auditoria. Tombstone deve informar
que a referência está indisponível, sem redirecionar para texto novo. Sua própria retenção é P01.

## Falhas versionadas

Erro usa Problem Details com `type`, `title`, `status`, `detail` sanitizado e extensões
`code`, `correlationId`, `contractVersion`. Não incluir stack, segredo, bytes ou texto fiscal.

| HTTP | Códigos da aplicação |
| --- | --- |
| 400 | INVALID_METADATA, INVALID_ARGUMENT |
| 401 / 403 | AUTHENTICATION_REQUIRED / PERMISSION_DENIED |
| 404 | RESOURCE_NOT_FOUND |
| 409 | VERSION_CONTENT_CONFLICT, IDEMPOTENCY_CONFLICT, INVALID_STATE, ACTIVE_DEPENDENCY, PROFILE_INCOMPATIBLE |
| 412 / 428 | REVISION_MISMATCH / PRECONDITION_REQUIRED |
| 413 / 415 / 422 | FILE_TOO_LARGE / UNSUPPORTED_FORMAT / PDF_TEXT_UNAVAILABLE |
| 409 | RETENTION_NOT_CONFIGURED, RETENTION_NOT_EXPIRED — expurgo negado sem efeito |
| 429 | ADMISSION_LIMIT — respeitar Retry-After quando informado |
| 503 / 504 | DEPENDENCY_UNAVAILABLE / OPERATION_TIMEOUT |

Limites, timeout, tentativas e lease são configuração finita obrigatória validada no startup.
Não inferir timeout técnico de 5s a partir da meta estatística de aceite. Retry transitório
limitado não repete efeitos já concluídos e registra cada tentativa de embeddings. Cancelamento
é propagado, mas resposta cancelada não prova ausência de efeito; cliente consulta a operação.

## Testes do contrato

Validar payload, rejeitar token/permissão inválidos, replay e concorrência de ingestão,
conflito de bytes, aprovação pelo autor permitida, fonte não aprovada fora da busca,
reprocessamento/retirada concorrentes, expurgo sem prazo/antes do prazo/sem permissão negado
e expurgo permitido auditado. Validar descoberta com/sem knowledge.audit, lista vazia/versão
inexistente, política ausente, nenhum conteúdo no DTO e revisão do item utilizada no If-Match;
mudança concorrente entre consulta e comando exige rejeição 412 sem exclusão. P01/P04 devem
estar preenchidos em ambiente de teste com dados
sintéticos; corpus oficial e configuração produtiva continuam sujeitos a seus gates.
