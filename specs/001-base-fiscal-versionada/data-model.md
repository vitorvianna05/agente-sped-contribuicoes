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
- Mesmo par e mesmos bytes: reutilizar conteúdo/operação conforme idempotência, sem duplicata ativa.
- Alteração do PDF exige outra versão oficial; reprocessamento não troca bytes da origem.
- Aplicabilidades são registros filhos: `documentType`, `layoutVersion`, `officialBasis`,
  `recordedBy`, `recordedAt`; alteração exige permissão administrativa e nova revisão.
  Não inferir compatibilidade pela versão do manual, proximidade textual ou data.
- Aprovação inicial exige geração concluída e íntegra. Ator pode ser o mesmo que ingeriu.
- Estados de publicação: `unpublished`, `published`, `withdrawn`, `purged`. Não confundir
  com estado da operação. Fonte retirada não é restaurada por reprocessamento ou approve.
  Restauração não tem caso de uso nesta entrega; requer regra explícita antes de ser oferecida.

### GeracaoProcessamento

Entidade identificada e persistida separadamente, pertencente à versão: `generationId`,
`documentVersionId`, `operationId`, `profileId`, `sourceHash`, `state`, `createdAt`,
`completedAt?`, `supersededAt?`, `contentManifestHash?`, `failureCode?`.

`queued → processing → succeeded | failed | cancelled`. Conteúdo de geração concluída é
imutável. Apenas geração bem-sucedida pode ser ativada. Reprocessamento pode mudar extrator,
segmentação e embeddings, preservando hash da origem. Metadados de auditoria não reescrevem texto.
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
pelo trecho persistido. Após expurgo, tombstone retorna indisponibilidade e não outro texto.
Tombstone tem política própria em P01; inexistência posterior retorna `REFERENCE_NOT_FOUND`.

### PerfilProcessamento

Raiz imutável: `profileId`, `extractorVersion`, `normalizationVersion`, `segmentationStrategy`,
`segmentationParameters`, `embeddingProvider`, `embeddingModel`, `embeddingDimensions`,
`embeddingMetric`, `queryNormalizationVersion`, `retrievalParameters`, `evaluationId?`,
`adoptionState` (`candidate` ou `adopted`).

Estratégia e parâmetros são externos/configuráveis, mas versionados. Nenhum valor de chunk,
overlap, topK, limiar ou modelo final é aprovado neste documento. Adoção exige avaliação P02.
A query deve usar espaço de embedding compatível, não apenas vetor de mesma dimensão.
Persistir vetores com perfil e dimensão validados; busca exata sobre o perfil selecionado.
Mudança incompatível não permite misturar distâncias ou ativar gerações incompatíveis com
o perfil de busca do corpus. Migração de perfil exige plano e corte consistente do conjunto.

### ConfiguracaoCatalogo e avaliação isolada

Registro de controle `catalogId`, `activeProfileId`, `revision`: a busca captura perfil e
ponteiros ativos no mesmo snapshot. Troca de perfil do catálogo só ocorre após avaliação e
quando todas as versões publicadas a manter na busca têm geração íntegra correspondente;
se faltar uma, rejeitar ativação em vez de misturar espaços ou omitir fontes silenciosamente.
Não há endpoint público de migração automática nesta entrega; sua execução é operação técnica
controlada, versionada e validada em testes antes de adoção. Reprocessar com o mesmo perfil
adotado segue a troca automática aprovada pelo usuário.

Avaliação de candidato usa um snapshot isolado de gerações staged e expectativas aprovadas,
sem publicar o candidato nas tools operacionais. O harness de testes tem acesso interno apenas
ao ambiente isolado da Api.RAG, não fornece canal funcional alternativo à Api.Agente. Testes
sintéticos de contrato podem adotar um perfil fake explícito no ambiente de teste; isso não
é adoção de parâmetros de produção nem avaliação oficial de relevância.

### Operacao e TentativaConsumo

Operação: `operationId`, `type`, `documentVersionId?`, `generationId?`, `principalId`,
`correlationId`, `state`, `createdAt`, `startedAt?`, `finishedAt?`, `leaseUntil?`,
`leaseOwner?`, `attemptNumber`, `resultCode?`, `idempotencyKeyDigest`, `requestDigest`.

Chave de idempotência por principal e tipo de operação; reutilização com payload diferente
é conflito. Constraint no banco protege repetição concorrente. Fila/lease persistidos permitem
retomada; worker não depende de fila exclusivamente em memória. Efeitos são idempotentes,
mas chamada ambígua ao provedor pode gerar nova tentativa faturável, sempre registrada.

Consumo por tentativa: `attemptId`, `operationId`, `correlationId`, `service`, `provider`,
`model`, `startedAt`, `finishedAt?`, `status`, `inputTokens?`, `outputTokens?`,
`embeddingTokens?`, `usageAvailability`, `estimatedCost?`, `providerReportedCost?`,
`currency?`, `priceReference?`, `priceVersion?`. Custos em decimal, desconhecidos nulos.
Não persistir prompts ou PDF completo no evento. Intenção e desfecho são estados separados;
interrupção entre rede e banco deixa desfecho desconhecido explícito. Retry oculto de SDK
não pode omitir tentativas. Auditoria operacional fica na Api.RAG.

### RetencaoArtefato

`retainedArtifactId`, `artifactType`, `artifactId`, `eventType` (`withdrawn` ou `superseded`),
`eventAt`, `retentionCategory`, `policyVersion?`, `retentionDuration?`, `eligibleForExpurgeAt?`,
`expurgedAt?`, `expurgedBy?`, `expurgeReason?`, `revision`.

Valor ausente não significa zero. Sem prazo/política válida não há expurgo. Vencimento
apenas torna elegível; exige ação manual de administrador central autorizado, com motivo e
auditoria. Repetição não reinicia marco. Registrar versão da política e vencimento; alterações
não antecipam exclusão silenciosamente. Não oferecer recálculo retroativo sem política aprovada.
Expurgo não remove origem compartilhada com geração ativa ou outra retenção ainda vigente.
Políticas de consulta/auditoria/originais/tombstones são entradas P01 antes da persistência real.

Projeção administrativa de leitura, sem criar novo agregado: consulta por documentVersionId
retorna retainedArtifactId, category, state (`retained`/`purged`), eventType/eventAt,
policyVersion e eligibleForExpurgeAt nullable, eligibleForExpurge, impediments e revision.
São somente metadados; sem PDF, texto, embedding ou endereço do armazenamento. Revision é
token forte opaco por artefato, cobrindo revisão e metadados de elegibilidade projetados,
usável literalmente em If-Match; não é a revisão da lista ou da versão documental.
Ausência de política resulta em null nos valores desconhecidos, false na elegibilidade e
impedimento explícito. Knowledge.audit central permite leitura, não knowledge.expurge.
Recalcular o token/estado sob snapshot consistente no comando: revisão antiga retorna 412;
mesmo com revisão atual, autorização/prazo/dependências são revalidados antes da exclusão.

### Avaliacao e Manifesto

`evaluationId`, `manifestVersion`, fontes com hash/origem/versão/aplicabilidade, perguntas,
referências esperadas, negativos, rubrica, critérios, configurações candidatas, resultados,
`fiscalApprover`, `approvedAt`, `technicalExecutor`, `executedAt?`, ambiente, tamanho/páginas,
amostra e cache. Fiscal aprova conteúdo/expectativas; técnica organiza e executa. Resultado
não executado não pode ser aprovado por preenchimento de relatório vazio.

## Objetos de valor e portas

Objetos de valor: identidade oficial, versão oficial, hash, aplicabilidade, localização,
perfil de embedding e correlação. Validam formato/consistência, sem acessar banco ou HTTP.
Agregados protegem invariantes de publicação; objetos de valor não recebem identidade própria
sem necessidade. Serviços de domínio tratam elegibilidade e retenção; aplicação coordena
transações, permissões e chamadas às portas. Portas de persistência, PDF, embeddings, relógio e
auditoria pertencem à aplicação/domínio conforme responsabilidade; infraestrutura as implementa.
Não criar repositório genérico ou evento distribuído sem caso de uso demonstrado.

## Consistência e transições

| Evento | Condição | Efeito atômico |
| --- | --- | --- |
| Ingestão concluída | PDF suportado e origem consistente | Geração succeeded; publicação unpublished, sem ativação automática. |
| Aprovação inicial | Permissão central, revisão esperada e geração succeeded | published + ponteiro ativo + aprovador/data + auditoria. |
| Reprocessamento concluído | Hash original preservado, geração íntegra, versão ainda published, revisão/ponteiro esperados | Troca integral de ponteiro, evento superseded e auditoria; sem nova aprovação. |
| Falha/revisão concorrente | Geração incompleta, retirada ou estado mudou | Não trocar ponteiro nem reativar; registrar falha/superseded operation, manter histórico. |
| Retirada | Permissão central e revisão esperada | withdrawn, registrar marco, impedir novas buscas; resolver referências historicamente. |
| Expurgo | Permissão central específica, política válida vencida, artefato não ativo e sem dependência impeditiva | Remover conteúdo autorizado, tombstone/estado e auditoria; não executar por timer de vencimento. |

Autorização é revalidada no caso de uso; estado, prazo e revisão no momento do efeito.
Transações são curtas, nunca abertas durante PDF/embeddings. Constraint de unicidade e lock/CAS
protegem corridas. Consulta seleciona geração ativa e trechos no mesmo snapshot; busca em várias
leituras exige transação consistente. Resolução concorrente a expurgo devolve texto exato
consistente ou indisponibilidade, nunca conteúdo parcial ou de outra versão.

## Verificação e gates

FR-001–021 e QR-001–003 são verificados pelos cenários de quickstart.md; SC-009/010 exigem
protocolo com corpus de 300 PDFs e 5 consultas concorrentes. P01–P04 não estão satisfeitos por
este modelo. Não há migração, SQL executado, schema fiscal inventado ou código de aplicação.
