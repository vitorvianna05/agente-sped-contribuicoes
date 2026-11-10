# Modelo de domínio e dados — Conhecimento Fiscal

Desenho da feature 001 sob constitution 1.1.0. Campos são contratos de aplicação e persistência,
não schemas fiscais. Propriedade exclusiva da Api.RAG. Identificadores técnicos são UUIDs,
hashes SHA-256 calculados pelo servidor, datas UTC e versões oficiais strings opacas.

## Contexto e linguagem compartilhada

**Conhecimento Fiscal** administra evidências oficiais, aplicabilidade, processamento,
publicação e recuperação. **Análise Fiscal/Orquestração** consome evidências por MCP;
**Memória Conversacional** não é fonte oficial e não pertence a este modelo.
Fonte é a identidade documental; versão documental é a edição do manual/guia; versão de
layout é a do documento analisado. Aplicabilidade liga esses conceitos com fundamento oficial.
Geração é uma representação processada da mesma origem; não é outra versão oficial.

## Agregados e entidades

### FonteOficial

Raiz com `sourceId`, `issuer`, `officialIdentifier`, `title`, `kind` (`manual` ou `guide`),
`originUri`, `createdAt`. Unicidade por emissor e identificação oficial; origem verificável
registrada pelo administrador. URL é metadado, não autorização para baixar URLs arbitrárias.
Versões se vinculam por identidade, sem carregar todo o corpus como um agregado.

### VersaoDocumental

Raiz de consistência de publicação: `documentVersionId`, `sourceId`, `officialVersion`,
`originalContentHash`, `originalArtifactId`, `publicationState`, `activeGenerationId?`,
`approvedBy?`, `approvedAt?`, `withdrawnAt?`, `revision`, `createdAt`.

- Unicidade `(sourceId, officialVersion)` no banco. Mesmo par e hash diferente: rejeitar
  `VERSION_CONTENT_CONFLICT` sem alterar origem, versões, publicação ou ponteiro ativo.
- Mesmo par e mesmos bytes: reaproveitar a geração concluída/em andamento com trechos; se só
  houver gerações com falha ou sem trechos, criar nova geração (ADR 0009). Sem duplicata ativa.
- Alteração do PDF exige outra versão oficial; nova geração usa os bytes originais.
- Aplicabilidades são registros filhos: `documentType`, `layoutVersion`, `officialBasis`,
  `recordedBy`, `recordedAt`, gravados na primeira ingestão da versão.
  Não inferir compatibilidade pela versão do manual, proximidade textual ou data.
- Publicação é automática (`PublishGeneration`): geração `succeeded` com manifesto da própria
  versão vira a ativa e a versão passa a `published`; `approvedBy`/`approvedAt` registram autor e
  instante da primeira publicação. Nova geração concluída substitui a ativa.
- Publicar uma versão retira (`Withdraw`) as demais versões `published` da mesma fonte.
- Estados de publicação: `unpublished`, `published`, `withdrawn`, `purged` (`purged` só existe
  em dados anteriores à ADR 0009). Não confundir com estado da operação. Uma versão `withdrawn`
  volta a `published` se for reenviada e concluir (o envio mais recente vence).

### GeracaoProcessamento

Entidade identificada e persistida separadamente, pertencente à versão: `generationId`,
`documentVersionId`, `operationId`, `profileId`, `sourceHash`, `state`, `createdAt`,
`completedAt?`, `supersededAt?`, `contentManifestHash?`, `failureCode?`.

`queued → processing → succeeded | failed | cancelled`. Conteúdo de geração concluída é
imutável. Apenas geração bem-sucedida pode ser ativada; ao ser ativada recebe `activated_at`, e a
anterior recebe `superseded_at`. Estágios internos: `preparation_state` (texto) e
`embedding_state` (vetores), cada um com lease e limite de tentativas próprios.
O vínculo ativo é único por versão; gerações históricas não são elegíveis para novas buscas.

### Trecho e Referencia

`chunkId`, `generationId`, `ordinal`, `text`, `textHash`, `sourceSpans`, `location`,
`referenceId`, `createdAt`. Unicidade `(generationId, ordinal)` e `referenceId` global não reciclado.
Texto igual em ocorrências distintas não elimina proveniências. Cada referência fixa
fonte, versão, geração e trecho; nunca aponta para o trecho atualmente ativo por conveniência.

`location`: `availability` (`available` ou `unavailable`), `physicalPages` (índices físicos
observados, a partir de 1), `printedLabels`, `section`, `reason` quando indisponível.
Nenhuma página/seção inferida deve ser apresentada como observada. Página física e rótulo
impresso são conceitos separados. Com localização ausente, referência continua recuperável
pelo trecho persistido. A API não expurga conteúdo (ADR 0009); tombstones anteriores ainda
retornam `REFERENCE_PURGED` e referência inexistente retorna `REFERENCE_NOT_FOUND`.

### PerfilProcessamento

Raiz imutável: `profileId`, `extractorVersion`, `normalizationVersion`, `segmentationStrategy`,
`segmentationParameters`, `embeddingProvider`, `embeddingModel`, `embeddingDimensions`,
`embeddingMetric`, `queryNormalizationVersion`, `retrievalParameters`, `evaluationId?`,
`adoptionState` (`candidate` ou `adopted`).

Estratégia e parâmetros são externos/configuráveis, mas versionados (snapshot JSON por UUID e
hash em `rag.processing_profiles`; UUID igual com definição diferente é `PROFILE_INCOMPATIBLE`).
`adoptionState`/`evaluationId` são apenas validados quanto ao formato; não condicionam a busca.
Perfil de busca: `Retrieval:AdoptedProfileId` ou o único perfil configurado, compatível com
`Embeddings` (ADR 0009). `TopK` ausente = 5; `Threshold` ausente = sem corte. Nenhum valor de
chunk, overlap, topK ou limiar foi avaliado (desvio registrado na ADR 0009).
A query usa o mesmo espaço de embedding do perfil; busca exata por cosseno somente sobre
gerações desse perfil, sem misturar distâncias de perfis diferentes.

### Catálogo (removido)

`ConfiguracaoCatalogo` e o corte técnico de perfil foram removidos na ADR 0009. A tabela
`rag.catalog` permanece no esquema, sem uso. Testes sintéticos usam perfil `Fake` explícito,
o que não é avaliação oficial de relevância.

### Operacao e TentativaConsumo

Operação: `operationId`, `type`, `documentVersionId?`, `generationId?`, `principalId`,
`correlationId`, `state`, `createdAt`, `startedAt?`, `finishedAt?`, `leaseUntil?`,
`leaseOwner?`, `attemptNumber`, `resultCode?`, `idempotencyKeyDigest`, `requestDigest`.

Término não pode anteceder criação, inclusive quando startedAt é nulo; domínio e banco validam essa cronologia.
Não há chave de idempotência do cliente: `idempotencyKeyDigest` é derivado do próprio
`operationId` (único por envio) e `requestDigest` cobre metadados + hash do arquivo. A repetição
segura vem da identidade do documento (ver VersaoDocumental); a operação de um reenvio
reaproveitado é descartada na mesma transação. Fila/lease persistidos permitem retomada;
worker não depende de fila em memória. Chamada ambígua ao provedor pode gerar nova tentativa
faturável, sempre registrada.

Consumo por tentativa: `attemptId`, `operationId`, `correlationId`, `service`, `provider`,
`model`, `startedAt`, `finishedAt?`, `status`, `inputTokens?`, `outputTokens?`,
`embeddingTokens?`, `usageAvailability`, `estimatedCost?`, `providerReportedCost?`,
`currency?` (moeda informada pelo provedor), `estimatedCurrency?` (moeda da tabela da estimativa), `priceReference?`, `priceVersion?`. Custos em decimal, desconhecidos nulos. Moedas são independentes: a tabela não preenche moeda omitida pelo provedor e não há conversão presumida.
Não persistir prompts ou PDF completo no evento. Intenção e desfecho são estados separados;
interrupção entre rede e banco deixa desfecho desconhecido explícito. Retry oculto de SDK
não pode omitir tentativas. Auditoria operacional fica na Api.RAG.

### Retenção (sem expurgo)

`RetencaoArtefato`, `ObrigacaoRetencao`, `RetentionDecision`, a projeção de artefatos retidos e o
expurgo foram removidos (ADR 0009). A configuração `Retention` continua obrigatória para ingerir
(`PolicyVersion`, `ApprovalReference`, `OriginalDays`, `ReferenceDays`, `AuditDays`, `QueryDays`,
`ConsumptionDays` positivos; `AutomaticExpurgeEnabled=false`) e a versão da política é gravada
com o original, mas nenhum prazo dispara exclusão: todo conteúdo permanece até remoção externa.
As tabelas `retained_artifacts`, `retention_obligations`, `reference_tombstones` e
`command_results` permanecem no esquema, sem uso pela aplicação.

### Avaliacao e Manifesto

`evaluationId`, `manifestVersion`, fontes com hash/origem/versão/aplicabilidade, perguntas,
referências esperadas, negativos, rubrica, critérios, configurações candidatas, resultados,
`fiscalApprover`, `approvedAt`, `technicalExecutor`, `executedAt?`, ambiente, tamanho/páginas,
amostra e cache. Fiscal aprova conteúdo/expectativas; técnica organiza e executa. Resultado
não executado não pode ser aprovado por preenchimento de relatório vazio. Entidade planejada,
não implementada; após a ADR 0009 não condiciona publicação nem busca.

## Objetos de valor e portas

Objetos de valor: identidade oficial, versão oficial, hash, aplicabilidade, localização,
perfil de embedding e correlação. Validam formato/consistência, sem acessar banco ou HTTP.
Agregados protegem invariantes de publicação; objetos de valor não recebem identidade própria
sem necessidade. Domínio trata elegibilidade e publicação; aplicação coordena
transações, permissões e chamadas às portas. Portas de persistência, PDF, embeddings, relógio e
auditoria pertencem à aplicação/domínio conforme responsabilidade; infraestrutura as implementa.
Não criar repositório genérico ou evento distribuído sem caso de uso demonstrado.

## Consistência e transições

| Evento | Condição | Efeito atômico |
| --- | --- | --- |
| Texto preparado | PDF suportado e origem consistente | `preparation_state` ready, trechos persistidos; operação queued/EMBEDDINGS_PENDING; versão inalterada. |
| Vetores concluídos | Todos os trechos com vetor válido | Geração succeeded + manifesto; versão published com esta geração ativa (`activated_at`), anterior `superseded_at`; demais versões published da fonte → withdrawn; auditoria. |
| Falha | PDF ilegível, limite, timeout, provedor ou tentativas esgotadas | Geração/operação failed com código; ponteiro ativo e publicação inalterados. |
| Reenvio reaproveitado | Geração da mesma versão/perfil concluída ou em andamento, com trechos | Operação nova descartada; devolve a existente; publica a geração concluída se a versão não estiver published. |
| Reenvio após falha/trechos ausentes | Nenhuma geração reaproveitável | Nova operação + nova geração queued. |

Autorização é revalidada no caso de uso; estado no momento do efeito.
Transações são curtas, nunca abertas durante PDF/embeddings. Constraint de unicidade, lock
consultivo por identidade da fonte e lease protegem corridas. Consulta seleciona geração ativa e
trechos no mesmo snapshot (RepeatableRead), nunca conteúdo parcial ou de outra versão.

## Verificação e gates

Incremento textual dos ajustes 1–4: FonteOficial, VersaoDocumental, Aplicabilidade,
GeracaoProcessamento, PerfilProcessamento, Trecho e Localizacao possuem implementação,
com persistência em 0006_documents.sql. Referencia é o UUID global imutável vinculado ao
trecho na mesma linha; resolução MCP/tombstone foram acrescentados no incremento seguinte. Perfis são snapshots JSON
imutáveis por UUID/hash; aplicabilidades são filhos relacionais com ator/data do transporte.
Originais incluem a versão da política de retenção. preparationState e preparationManifestHash
registram o estágio textual separado da geração integral; ver ADR 0005.

FR-001–022 e QR-001–003 são verificados pelos cenários de quickstart.md; SC-009/010 exigem
protocolo com corpus de 300 PDFs e 5 consultas concorrentes. P01–P04 não estão satisfeitos por
este modelo. A Phase 2 implementa Operacao, consumo, auditoria e cinco migrações de fundação,
validadas no PostgreSQL isolado (validation/phase-2.md). O incremento textual adiciona a sexta
migração e os modelos citados acima. O incremento 5–11 adiciona migrações 0007/0008:
embedding por trecho, estado/lease vetorial separado, manifesto integral e activated_at (em uso),
além de catálogo, retenção, tombstones e command_results (sem uso após ADR 0009). A migração
0009_retention_obligations também permanece apenas por compatibilidade. Nenhuma migração foi
removida ou alterada: as nove continuam aplicadas na mesma ordem e com o mesmo checksum.
Avaliação/capacidade oficiais continuam pendentes; nenhum schema fiscal foi inventado.
