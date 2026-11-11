---
description: "Tarefas por história para a base fiscal versionada e rastreável"
---

# Tasks: Base de conhecimento fiscal versionada com recuperação rastreável

**Input**: documentos em `specs/001-base-fiscal-versionada/`.
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contratos REST](contracts/admin-rest.md),
[contratos MCP](contracts/mcp.md), [schemas](contracts/mcp-tools.schema.json) e
[quickstart.md](quickstart.md); constitution 1.1.0.
**Status (2026-11-10)**: 86 tasks — 59 concluídas, 22 pendentes e 5 canceladas. Modelo simplificado ([ADR 0009](adrs/0009-simplificacao-ingestao-busca.md)): ingestão com publicação automática, embeddings, busca e servidor MCP implementados; aprovação, retirada/reprocessamento administrativos, catálogo e expurgo removidos (tasks marcadas como *Supersedida*/*Cancelada*). Última evidência: 97 testes aprovados (ADR 0009). Matrizes integrais, avaliação, capacidade, recuperação/backup e gates P02–P04 continuam pendentes. [Mapa de sincronização](validation/sdd-synchronization.md).
**Tests**: incluídos porque spec, critérios de aceite, QR-001–003 e DoD exigem testes
determinísticos, contratos, integração, avaliação e capacidade. Testes de cada incremento
devem ser escritos antes da implementação correspondente, demonstrar falha de comportamento
com fixture válida quando os contratos compilarem e passar antes de concluir o incremento.

**Organization**: organização primária pelas cinco histórias, na ordem P1/P1/P1/P2/P2.
As fases são incrementos de código; teste independente não significa ausência de componentes
compartilhados. US2–US5 podem usar fixtures preparadas sem executar novamente a jornada US1.

## Format: `[ID] [P?] [Story] Description`

- `[P]` indica tarefas do mesmo lote com arquivos distintos e predecessores concluídos.
- `[US1]`–`[US5]` identificam histórias da spec. Setup, fundação e fechamento não têm story.
- Todas as tarefas indicam arquivos concretos, critérios/requisitos e predecessores.
- Dependência é obrigatória; não executar tarefa só pela ordem visual se seu predecessor
  não concluiu. Tarefas documentais com insumos externos ausentes permanecem abertas/bloqueadas.

## Path Conventions

Caminhos são relativos à raiz do workspace. Produção: `src/Api.RAG/`; testes:
`tests/Api.RAG.Tests/`; artefatos/evidências: `specs/001-base-fiscal-versionada/`.
Manter domínio/aplicação/infraestrutura e adaptadores Controllers/Tools/Ingestao/Rag conforme DDD e o plano revisado. Não criar Api.Agente nesta feature (ela está planejada na feature 002),
OCR, schemas fiscais, LLM gerador ou serviços extras para satisfazer esta feature.

## Condições externas e execução segura dos incrementos

| Gate do plano | Como trabalhar sem inventar uma decisão | Momento bloqueado |
| --- | --- | --- |
| P01 | Encerrado pela ADR 0009: a API não exclui conteúdo; a configuração de retenção é exigida só como registro. T075 cancelada. | — |
| P02 | T069 registra aprovação fiscal, T077 executa a avaliação. Desde a ADR 0009 a avaliação não condiciona a busca; não cadastrar manual oficial, resposta esperada ou métrica por suposição. | Declarar parâmetros como avaliados. |
| P03 | T072 registra protocolo; T078 só conclui com 300 PDFs caracterizados e ensaio real. | Aceite SC-009/010 e comprovação de dimensionamento. |
| P04 | Credenciais/identidades fake só no ambiente isolado; T004 valida versões, T076 valida ambiente real autorizado. | Integração/exposição real sem configuração e evidências. |

As referências T069/T072/T077/T078 são detalhadas nas fases abaixo. Não incluir segredos ou
conteúdo fiscal sensível em exemplos, manifestos públicos, logs ou Git. Falta de insumo externo
não bloqueia escrever código parametrizado e testar com fixtures; bloqueia sua ativação real
e a conclusão da tarefa que exige esse insumo. Não aprovar produção por concluir o desenho.


**Estado sincronizado**: a estrutura da Api.Conhecimento/Api.Atendimento foi adaptada e
implementada na Api.RAG. A rota é `/api/v1/ingest`; tools `BuscarConhecimento` e
`ResolverReferencia` estão em `/mcp`. T001–T043 estão marcadas concluídas; US3/US4 possuem
implementações e testes parciais de suas matrizes; US5 e fechamento permanecem abertos.
As seções históricas ao final registram o estado em cada incremento, não o estado atual.
JWT dispensado somente no Development loopback autorizado; P04 continua aberto.
Modelos escolhidos: text-embedding-3-small/1536 e gpt-4.1-mini para a Api.Agente
([ADR 0010](adrs/0010-ambiente-local-openai-swagger.md)), sem aprovação fiscal/qualidade presumida.
Desde 2026-11-10 (ADR 0009) não há aprovação, Idempotency-Key, If-Match, catálogo nem expurgo;
P01 deixou de bloquear (sem exclusão) e P02 não condiciona a busca. T084 permanece pendente.

## Phase 1: Setup (Shared Infrastructure)

**Goal**: Inicializar a solução e as convenções, sem adicionar serviços ou bibliotecas de produção sem justificativa.

**Independent Test**: SDK .NET 10 selecionado; projetos restauráveis; configuração sintética isolada e documentação de gates rastreável.

- [X] T001 Criar solução AgenteSpedContrib, projeto web net10.0 e projeto xUnit, selecionando SDK estável .NET 10 em global.json; manter somente Api.RAG e um projeto de testes nesta feature. Arquivos: `AgenteSpedContrib.slnx`, `global.json`, `src/Api.RAG/Api.RAG.csproj`, `tests/Api.RAG.Tests/Api.RAG.Tests.csproj`. Depende de: nenhuma. Rastreio: QR-001–003; constitution §3.
- [X] T002 [P] Configurar nomes, formatação e analisadores .NET, documentando regras habilitadas; não inventar limites de linhas/classes como critérios aprovados. Arquivos: `.editorconfig`, `Directory.Build.props`. Depende de: T001. Rastreio: QR-002.
- [X] T003 [P] Documentar contexto Conhecimento Fiscal e direção de dependências; usar fonte, versão documental, versão de layout, aplicabilidade, geração, trecho, referência e publicação sem sinônimos contraditórios. Arquivos: `docs/architecture/conhecimento-fiscal.md`. Depende de: T001. Rastreio: QR-001, QR-003.
- [X] T004 Verificar em fontes oficiais a combinação compatível de Npgsql, Pgvector, PdfPig, cliente OpenAI, SDK MCP e testes; fixar versões efetivamente restauradas e registrar diferença frente às versões pesquisadas, sem adotar geração de parecer/Semantic Kernel na Api.RAG. Arquivos: `Directory.Packages.props`, `src/Api.RAG/packages.lock.json`, `tests/Api.RAG.Tests/packages.lock.json`, `specs/001-base-fiscal-versionada/research.md`. Depende de: T001, T002. Rastreio: P04; research R05–R11.
- [X] T005 Preparar host de testes, PostgreSQL/PgVector isolado, cliente MCP real em processo separado, relógio/provider fake e categorias Unit, Contract, Integration, Evaluation e Capacity; desligar dependência de serviços reais em testes determinísticos. Arquivos: `tests/Api.RAG.Tests/Fixtures/RagTestHost.cs`, `tests/Api.RAG.Tests/Fixtures/PostgresFixture.cs`, `tests/Api.RAG.Tests/Fixtures/McpConsumerProcess.cs`. Depende de: T004. Rastreio: plan Testing; quickstart.
- [X] T006 Criar registro operacional P01–P04 com responsáveis, estado pendente e evidências exigidas; fornecer exemplo sem segredos da configuração e valores apenas sintéticos para testes, sem aprovar política real ou corpus por preenchimento. Arquivos: `specs/001-base-fiscal-versionada/validation/gates.md`, `src/Api.RAG/appsettings.Example.json`, `tests/Api.RAG.Tests/Fixtures/synthetic-settings.json`. Depende de: T004. Rastreio: P01–P04.

**Checkpoint**: validar as saídas da fase e as dependências antes do próximo incremento.

## Phase 2: Foundational (Blocking Prerequisites)

**Goal**: Disponibilizar contratos comuns, persistência, segurança e instrumentação reutilizáveis. Esta fase bloqueia todas as histórias.

**Independent Test**: Testes de configuração, autorização, idempotência e métricas passam no ambiente sintético; nenhuma dependência externa do domínio.

- [X] T007 Escrever testes dos contratos comuns e invariantes de configuração: ausência de prazo impede expurgo, perfil ausente não vira evidência insuficiente, identidade não vem do corpo e métricas desconhecidas não viram zero. Arquivos: `tests/Api.RAG.Tests/Unit/ConfigurationRulesTests.cs`, `tests/Api.RAG.Tests/Contracts/TransportEnvelopeTests.cs`. Depende de: T005, T006. Rastreio: FR-015–019; constitution §2.10.
- [X] T008 Implementar configuração externa validada para conexão, autenticação, limites finitos, timeout/retry/lease, perfis e retenção; ausências de políticas bloqueiam persistência operacional correspondente/expurgo, mas testes usam política sintética explícita. Arquivos: `src/Api.RAG/Infrastructure/Configuration/RagOptions.cs`, `src/Api.RAG/Infrastructure/Configuration/RagOptionsValidator.cs`. Depende de: T007. Rastreio: P01, P04.
- [X] T009 Implementar DTOs e validação comum conforme schemas, citando os obrigatórios de Result: `contractVersion,correlationId,status,evidences,limitations,error`; versão literal 1.0, correlação do sistema, dados desconhecidos nulos e propriedades extras rejeitadas; não confundir envelope MCP com JSON de LLM. Arquivos: `src/Api.RAG/Endpoints/Contracts/KnowledgeResult.cs`, `src/Api.RAG/Endpoints/Contracts/ContractValidator.cs`. Depende de: T007, T008. Rastreio: FR-009, FR-018; contracts/mcp-tools.schema.json.
- [X] T010 Implementar Problem Details versionado com códigos do contrato, correlação/trace e logs sanitizados; distinguir erros de protocolo, execução e negócio e propagar cancelamento sem alegar que cancelamento desfaz efeito. Arquivos: `src/Api.RAG/Endpoints/Middleware/ProblemDetailsHandler.cs`, `src/Api.RAG/Infrastructure/Observability/CorrelationMiddleware.cs`. Depende de: T009. Rastreio: FR-008, FR-016, FR-018.
- [X] T011 Implementar validação Bearer de assinatura/emissor/audience/validade e políticas knowledge.read/ingest/publish/reprocess/withdraw/expurge/audit; atribuição central explícita, validação de Origin/hosts e HTTPS/metadados do recurso protegido para MCP; nunca herdar administração central do papel organizacional. Arquivos: `src/Api.RAG/Infrastructure/Security/KnowledgeAuthorization.cs`, `src/Api.RAG/Infrastructure/Security/AuthenticatedActor.cs`. Depende de: T008, T010. Rastreio: FR-015, FR-017; P04.
- [X] T012 Definir portas pequenas de persistência, PDF, embeddings, relógio e auditoria e objetos de valor compartilhados; preservar a regra “Identificadores técnicos são UUIDs, hashes SHA-256 calculados pelo servidor, datas UTC e versões oficiais strings opacas.”; validar sem banco/HTTP. Arquivos: `src/Api.RAG/Application/Knowledge/Ports/IKnowledgeUnitOfWork.cs`, `src/Api.RAG/Application/Knowledge/Ports/IPdfTextExtractor.cs`, `src/Api.RAG/Application/Knowledge/Ports/IEmbeddingProvider.cs`, `src/Api.RAG/Application/Knowledge/Ports/IAuditWriter.cs`, `src/Api.RAG/Domain/Knowledge/Values/IdentidadeOficial.cs`, `src/Api.RAG/Domain/Knowledge/Values/VersaoOficial.cs`, `src/Api.RAG/Domain/Knowledge/Values/ContentHash.cs`. Depende de: T008. Rastreio: QR-001–002; data-model Objetos de valor.
- [X] T013 Configurar Npgsql/Pgvector, runner de migrações versionadas e comando explícito com identidade de migração separada da aplicação; banco lógico da Api.RAG, extensão vector e transações curtas, sem credencial da Api.Agente. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/RagDataSource.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/MigrationRunner.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0001_infrastructure.sql`. Depende de: T012, T004. Rastreio: P04; D11.
- [X] T014 Modelar Operacao com todos os campos de data-model, incluindo `documentVersionId?`, `generationId?`, `startedAt?`, `finishedAt?`, `leaseUntil?`, `leaseOwner?`, `resultCode?`; preservar “Chave de idempotência por principal e tipo de operação; reutilização com payload diferente é conflito.” e referências técnicas não nulas restantes. Arquivos: `src/Api.RAG/Domain/Knowledge/Operacao.cs`. Depende de: T012. Rastreio: FR-010, FR-018.
- [X] T015 Implementar repositório de operações/idempotência e controle ETag/If-Match; chave por principal/operação e digest do pedido com constraint concorrente, 409 por outro payload, 428 sem precondição e 412 por revisão divergente; replay confirmado não repete efeitos. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/OperationRepository.cs`, `src/Api.RAG/Application/Knowledge/Concurrency/IdempotencyCoordinator.cs`, `src/Api.RAG/Endpoints/Contracts/RevisionPreconditions.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0002_operations.sql`. Depende de: T013, T014. Rastreio: FR-010, FR-016, FR-018. **Supersedida em 2026-11-10 (ADR 0009):** Idempotency-Key, ETag/If-Match e IdempotencyCoordinator/RevisionPreconditions removidos; repetição segura passou à identidade do documento.
- [X] T016 Persistir TentativaConsumo por tentativa: `finishedAt?`, `inputTokens?`, `outputTokens?`, `embeddingTokens?`, `estimatedCost?`, `providerReportedCost?`, `currency?`, `estimatedCurrency?`, `priceReference?`, `priceVersion?`; preservar “Custos em decimal, desconhecidos nulos.”; registrar intenção/desfecho, lacunas e retries internos sem prompts/PDF em eventos. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Embeddings/MeasuredEmbeddingProvider.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/ConsumptionRepository.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0003_consumption.sql`. Depende de: T015, T012. Rastreio: constitution §2.10; P01.
- [X] T017 Implementar auditoria de operações permitidas/negadas e consultas com ator autenticado, ação/contexto, data, resultado e correlação; efeitos mutáveis e auditoria na mesma transação; política própria antes de dados reais e leitura administrativa sem vazamento entre usuários. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Observability/KnowledgeAuditWriter.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0004_audit.sql`. Depende de: T016, T011. Rastreio: FR-016; P01.
- [X] T018 Registrar portas/adaptadores no ponto de composição, grupos REST administrativos e transporte MCP stateless sem tools de escrita ou consulta REST funcional; executar os testes de fundação, mantendo capacidades de histórias ainda não registradas. Arquivos: `src/Api.RAG/Program.cs`, `src/Api.RAG/Endpoints/KnowledgeEndpointRegistration.cs`, `tests/Api.RAG.Tests/Integration/FoundationTests.cs`. Depende de: T017, T007, T009. Rastreio: FR-017; P04; QR-001–003.

**Checkpoint**: validar as saídas da fase e as dependências antes do próximo incremento.

**Revisão da Phase 2 (2026-11-05)**: T014/T016/T011 corrigidas com cronologia validada no domínio/banco, cálculo de consumo no domínio, moedas independentes e ator autenticado utilizado. Migração aditiva `0005_foundation_review.sql`; numeração dos arquivos futuros de T026/T037/T054/T068 ajustada sem alterar seu escopo ou estado. Evidências: [validation/phase-2-review.md](validation/phase-2-review.md).

## Phase 3: User Story 1 - Ingerir fontes oficiais identificadas (Priority: P1) — MVP administrativo

**Goal**: Ingerir manual/guia PDF textual, acompanhar processamento e produzir trechos rastreáveis; disponibilizar automaticamente ao concluir texto e vetores (ADR 0009; antes: aprovação central explícita).

**Independent Test**: Com PDF sintético e perfil fake documentado: ingestão com vetores concluídos fica published, sem vetores fica unpublished, trechos preservam fonte/localização e entradas inválidas não publicam.

- [X] T019 [P] [US1] Escrever testes do multipart, acompanhamento e aprovação inicial, com campos/códigos de admin-rest.md, 202 distinto de published, extração falha e recusa de schema/OCR/outro formato; provar negação antes de mutação. Arquivos: `tests/Api.RAG.Tests/Contracts/IngestionContractTests.cs`. Depende de: T018. Rastreio: FR-001–004, FR-013, FR-018. **Atualização 2026-11-10 (ADR 0009):** aprovação inicial e Idempotency-Key saíram do contrato; testes cobrem convergência de envios concorrentes sem chave.
- [X] T020 [P] [US1] Escrever testes de imutabilidade da origem, identidade/versão, estados de processamento/publicação, referências e localização ausente; fixtures são sintéticas e não representam regra fiscal oficial. Arquivos: `tests/Api.RAG.Tests/Unit/DocumentInvariantsTests.cs`, `tests/Api.RAG.Tests/Fixtures/Documents/SyntheticPdfFactory.cs`. Depende de: T018. Rastreio: FR-002–004, QR-001.
- [X] T021 [US1] Modelar FonteOficial com `sourceId`, `issuer`, `officialIdentifier`, `title`, `kind` (`manual` ou `guide`), `originUri`, `createdAt`; aplicar “Unicidade por emissor e identificação oficial” e “URL é metadado, não autorização para baixar URLs arbitrárias.”; strings obrigatórias não vazias conforme contrato. Arquivos: `src/Api.RAG/Domain/Knowledge/FonteOficial.cs`. Depende de: T019, T020. Rastreio: FR-001–003.
- [X] T022 [US1] Modelar VersaoDocumental com todos os campos do modelo, opcionais `activeGenerationId?`, `approvedBy?`, `approvedAt?`, `withdrawnAt?`, e filhos `documentType`, `layoutVersion`, `officialBasis`, `recordedBy`, `recordedAt`; citar “Unicidade `(sourceId, officialVersion)` no banco.” e estados `unpublished`, `published`, `withdrawn`, `purged`; hash divergente gera VERSION_CONTENT_CONFLICT sem alterar bytes ou publicação. Arquivos: `src/Api.RAG/Domain/Knowledge/VersaoDocumental.cs`, `src/Api.RAG/Domain/Knowledge/Aplicabilidade.cs`. Depende de: T021. Rastreio: FR-002, FR-006, FR-011, FR-013.
- [X] T023 [US1] Modelar GeracaoProcessamento com todos os campos previstos, opcionais `completedAt?`, `supersededAt?`, `contentManifestHash?`, `failureCode?`; implementar “`queued → processing → succeeded | failed | cancelled`” e “Conteúdo de geração concluída é imutável.”; apenas succeeded pode ativar. Arquivos: `src/Api.RAG/Domain/Knowledge/GeracaoProcessamento.cs`. Depende de: T022. Rastreio: FR-001, FR-012, FR-018.
- [X] T024 [US1] Modelar Trecho/Referencia e Localizacao com os campos do modelo; aplicar “Unicidade `(generationId, ordinal)` e `referenceId` global não reciclado.”, `availability` (`available` ou `unavailable`), `physicalPages` a partir de 1, `section`/`reason` nullable conforme schema; unavailable exige reason, zero páginas/labels e section null; available exige localizador observado. Arquivos: `src/Api.RAG/Domain/Knowledge/Trecho.cs`, `src/Api.RAG/Domain/Knowledge/Values/Localizacao.cs`. Depende de: T023. Rastreio: FR-004, FR-009.
- [X] T025 [US1] Modelar PerfilProcessamento com todos os campos do modelo, `evaluationId?` e `adoptionState` (`candidate` ou `adopted`); preservar “Nenhum valor de chunk, overlap, topK, limiar ou modelo final é aprovado neste documento.”; validar dimensão/espaço/métrica sem substituir avaliação P02. Arquivos: `src/Api.RAG/Domain/Knowledge/PerfilProcessamento.cs`. Depende de: T024. Rastreio: FR-020–021; D04.
- [X] T026 [US1] Implementar schema e repositório de fontes/versões/aplicabilidades/originais bytea/gerações/trechos/perfis, com todas as constraints/nullable de T021–T025 e locks de revisão; separar versões oficiais, ocorrências textuais e gerações sem sobrescrever referências. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/DocumentRepository.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0006_documents.sql`. Depende de: T025, T013. Rastreio: FR-002–004, FR-010–013; D11.
- [X] T027 [US1] Implementar adaptador PdfPig por página/ordem de leitura, validação MIME/assinatura/limites e rejeição de documento sem texto útil ou dependente de OCR; preservar página física observada separada do rótulo impresso e ausência explícita, sem execução de instruções do PDF. Arquivos: `src/Api.RAG/Ingestao/PdfTextExtractor.cs`, `src/Api.RAG/Application/Knowledge/Ingestao/ValidateDocumentInput.cs`. Depende de: T026. Rastreio: FR-001–004, FR-019.
- [X] T028 [US1] Implementar segmentação determinística por estrutura/ocorrência e candidato de janela de tokens, com spans e perfis versionados; parâmetros somente de configuração candidata de teste/experimento, sem default de produção ou deduplicação global que apague proveniência. Arquivos: `src/Api.RAG/Ingestao/StructuralSegmenter.cs`, `src/Api.RAG/Ingestao/TokenWindowSegmenter.cs`. Depende de: T027, T025. Rastreio: FR-004, FR-010, FR-020–021.
- [X] T029 [US1] Implementar adaptador OpenAI de embeddings com modelo/dimensão externos compatíveis com perfil, timeout/cancelamento e retries limitados, instrumentado por tentativa; sem LLM gerador na Api.RAG e sem valores de qualidade presumidos, fake provider só em testes. Arquivos: `src/Api.RAG/Rag/OpenAiEmbeddingService.cs`. Depende de: T028, T016. Rastreio: constitution §2.10; FR-018; P02, P04.
- [X] T030 [US1] Implementar caso de uso de ingestão: hash do servidor, idempotência, conflito por bytes, origem/aplicabilidade identificadas e enfileiramento durável; mesma entrada não duplica trecho e origem inválida não se torna consultável. Arquivos: `src/Api.RAG/Application/Knowledge/Ingestao/IngerirFonte.cs`, `src/Api.RAG/Application/Knowledge/Ports/IDocumentStore.cs` (fila durável em DocumentRepository). Depende de: T029, T015, T017. Rastreio: FR-001–004, FR-010–011.
- [X] T031 [US1] Implementar worker hospedado com lease persistente, retomada e estados explícitos, escopo por operação e sem transação durante PDF/rede; escrever geração staged íntegra e finalizar sem publicação inicial automática, preservando falha/cancelamento e todas as tentativas. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Processing/IngestionWorker.cs`, `src/Api.RAG/Application/Knowledge/Ingestao/ProcessarGeracao.cs`. Depende de: T030. Rastreio: FR-001, FR-003, FR-018. **Atualização 2026-11-10 (ADR 0009):** a finalização agora publica automaticamente após os vetores (ADR 0009).
- [X] T032 [US1] Implementar aprovação inicial explícita: `published + ponteiro ativo + aprovador/data + auditoria`, revisão esperada, geração íntegra e perfil adotado; autor da ingestão pode aprovar; fonte withdrawn não é restaurada; perfil fake adotado apenas em ambiente sintético. Arquivos: `src/Api.RAG/Application/Knowledge/Publicacao/AdministrarConhecimento.cs`. Depende de: T031, T011, T017. Rastreio: FR-013, FR-015–016. **Supersedida em 2026-11-10 (ADR 0009):** aprovação manual removida; publicação automática ao concluir vetores (KnowledgePublication).
- [X] T033 [US1] Expor POST /api/v1/ingest, GET operations/{id}, GET document-versions/{id} e POST approve, validando metadados obrigatórios, officialVersion string opaca e permissões específicas; encadear ETag/If-Match/Idempotency-Key e acompanhar estados distintos. Arquivos: `src/Api.RAG/Controllers/IngestController.cs`, `src/Api.RAG/Controllers/KnowledgeLifecycleController.cs`, `src/Api.RAG/Application/Knowledge/Ingestao/IngestionMetadata.cs`. Depende de: T032, T010. Rastreio: FR-001–004, FR-013, FR-018. **Supersedida em 2026-11-10 (ADR 0009):** POST approve removido; POST /api/v1/ingest sem Idempotency-Key e GETs sem ETag.
- [X] T034 [US1] Executar testes de ingestão/retomada/aprovação no PostgreSQL e registrar comandos/resultado; provar recusa de PDF parcial/OCR, localização não fabricada e ausência de publicação antes de approve; manter dados e perfil explicitamente sintéticos. Arquivos: `tests/Api.RAG.Tests/Integration/IngestionJourneyTests.cs`, `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/rag-complete.md`. Depende de: T033, T019, T020. Rastreio: SC-003, SC-005–007; P01/P04 de teste. **Atualização 2026-11-10 (ADR 0009):** "ausência de publicação antes de approve" passou a "ausência de publicação antes dos vetores".

**Checkpoint**: validar US1 com os testes e registrar resultado antes de marcá-la concluída.

## Phase 4: User Story 2 - Recuperar evidências da versão aplicável (Priority: P1)

**Goal**: Recuperar por MCP somente fontes/versões/gerações/perfis elegíveis, com referências exatas.

**Independent Test**: Sem executar ingestão no teste, semear versões semelhantes publicadas com perfil fake; cliente MCP deve receber só a aplicável e resolver o trecho exato.

- [X] T035 [P] [US2] Escrever testes dos inputSchema/outputSchema das duas tools: SearchInput exige `consulta,tipoDocumento,versaoLayout`, ResolveInput exige `referenceId`; rejeitar extras como topK/ator/bypass; validar obrigatórios e adicionais false em Result/Evidence/Location. Arquivos: `tests/Api.RAG.Tests/Contracts/McpSchemaTests.cs`. Depende de: T034. Rastreio: FR-005–006, FR-009, FR-017.
- [X] T036 [P] [US2] Escrever testes de seleção relacional contra versões próximas com texto idêntico, unpublished/withdrawn e gerações históricas; distinguir versão oficial de manual e layout, com fixture sem dependência da jornada de ingestão. Arquivos: `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`, `tests/Api.RAG.Tests/Integration/LifecycleCorrectionsTests.cs`. Depende de: T034. Rastreio: FR-005–006; SC-001. **Atualização 2026-11-10 (ADR 0009):** LifecycleCorrectionsTests removido; seleção por versão/layout coberta em CompleteRagJourneyTests.
- [X] T037 [US2] Modelar ConfiguracaoCatalogo com `catalogId`, `activeProfileId`, `revision`, snapshot consistente de perfil/ponteiros; proibir ativação parcial que misture espaços e exigir todas as versões publicadas mantidas na busca migradas antes de trocar perfil. Arquivos: `src/Api.RAG/Domain/Knowledge/ConfiguracaoCatalogo.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0007_catalog.sql`. Depende de: T035, T036. Rastreio: FR-005–006, FR-012; data-model ConfiguracaoCatalogo. **Supersedida em 2026-11-10 (ADR 0009):** ConfiguracaoCatalogo removida; migração 0007 mantida por compatibilidade.
- [X] T038 [US2] Implementar busca exata PgVector/cosseno com SQL parametrizado e filtros de tipo/layout/aplicabilidade documentada, estado published, geração ativa e perfil, no mesmo snapshot; nunca apenas pós-filtrar ou confiar em instruções ao LLM. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetrieval.cs`. Depende de: T037, T026. Rastreio: FR-005–006; SC-001.
- [X] T039 [US2] Implementar caso de uso de busca com identidade do transporte, embedding compatível e parâmetros de perfil adotado do servidor; construir texto/referência/limitações e nunca apresentar score como probabilidade fiscal. Arquivos: `src/Api.RAG/Application/Knowledge/Consulta/ConsultarConhecimento.cs`, `src/Api.RAG/Rag/ConhecimentoService.cs`. ConhecimentoService deve coordenar busca/resolução para as tools, sem duplicar elegibilidade ou SQL. Depende de: T038, T029. Rastreio: FR-005–009, FR-019. **Atualização 2026-11-10 (ADR 0009):** perfil de busca é o configurado (Retrieval:AdoptedProfileId ou único perfil), sem exigir adoção.
- [X] T040 [US2] Implementar resolução por referência imutável com fonte/versão/geração/trecho, nunca busca por similaridade ou redirecionamento ao ativo; devolver no máximo um trecho e origem/location reais, com formato consistente de indisponibilidade. Arquivos: `src/Api.RAG/Application/Knowledge/Consulta/ConsultarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetrieval.cs`. Depende de: T039. Rastreio: FR-004, FR-009, FR-014.
- [X] T041 [US2] Registrar BuscarConhecimento e ResolverReferencia com schemas completos resolvidos e autorização knowledge.read por chamada, structuredContent validado e transporte Streamable HTTP stateless; sem canal REST funcional, acesso direto de consumidor ao banco ou escrita por tool. Arquivos: `src/Api.RAG/Tools/BuscarConhecimentoTool.cs`, `src/Api.RAG/Tools/ResolverReferenciaTool.cs`. Depende de: T040, T011, T009. Rastreio: FR-015, FR-017.
- [X] T042 [US2] Implementar cliente de teste separado que descobre/lista/chama tools, valida schemas/semântica e rejeita referência/resultado inválido; verificar metadata/versão compatível do SDK selecionado e falha sem fallback REST. Arquivos: `tests/Api.RAG.Tests/Fixtures/McpConsumerProcess.cs`, `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`. Depende de: T041, T005. Rastreio: FR-009, FR-017–018; P04.
- [X] T043 [US2] Executar ponta a ponta de recuperação e referência com fixture pré-publicada: zero versões incompatíveis, nenhuma mistura de gerações e conhecimento acessível só por MCP; registrar os comandos e evidências, sem presumir corpus oficial avaliado. Arquivos: `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/rag-complete.md`, `specs/001-base-fiscal-versionada/validation/review-corrections.md`. Depende de: T042, T036. Rastreio: SC-001, SC-003, SC-008.

**Checkpoint**: validar US2 com os testes e registrar resultado antes de marcá-la concluída.

## Phase 5: User Story 3 - Reconhecer falta de evidência e falhas (Priority: P1)

**Goal**: Distinguir limitação de conhecimento, contexto insuficiente e falha técnica sem fabricar conclusões.

**Independent Test**: Usar base preparada e provider controlado para casos sem resposta, evidência parcial, timeout e indisponibilidade; conferir status/limitations/error/isError e ausência de conformidade.

- [ ] T044 [P] [US3] Escrever testes das regras de Result: `evidence_found`, `insufficient_evidence`, `invalid_context`, `reference_unavailable`, `technical_failure`; error null exceto technical_failure, limitations não vazia, evidences vazia onde exigido e nenhuma referência inventada. Arquivos: `tests/Api.RAG.Tests/Contracts/KnowledgeOutcomeTests.cs`. Depende de: T043. Rastreio: FR-007–009, FR-018.
- [ ] T045 [P] [US3] Escrever testes de timeout/cancelamento, dependência indisponível, métricas desconhecidas e retentativas limitadas, distinguindo perfil ausente de busca concluída vazia e não repetindo erro de permissão/argumento. Arquivos: `tests/Api.RAG.Tests/Integration/RecoveryFailureTests.cs`. Depende de: T043. Rastreio: FR-008, FR-018; constitution §2.10.
- [X] T046 [US3] Implementar classificação de limitação/contexto e evidência parcial conforme perfil avaliado, sem usar distância como suficiência fiscal; sem regra avaliada, declarar cobertura não aferida e impedir adoção de parâmetros em produção. Arquivos: `src/Api.RAG/Application/Knowledge/Ports/IKnowledgeStore.cs`, `src/Api.RAG/Application/Knowledge/Consulta/ConsultarConhecimento.cs`, `src/Api.RAG/Rag/ConhecimentoService.cs`, `src/Api.RAG/Endpoints/Contracts/ContractValidator.cs`. Depende de: T044, T045. Rastreio: FR-005, FR-007, FR-020.
- [X] T047 [US3] Implementar execução resiliente de consulta/provider/banco com limites externos, cancelamento e retries auditados; mapear falhas a technical_failure/isError, erros de protocolo separados e nenhuma substituição silenciosa por REST ou outra versão. Arquivos: `src/Api.RAG/Application/Knowledge/Consulta/ConsultarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Embeddings/MeasuredEmbeddingProvider.cs`, `src/Api.RAG/Infrastructure/Configuration/RagOptionsValidator.cs`. Depende de: T046. Rastreio: FR-008, FR-017–018.
- [X] T048 [US3] Testar e fechar rotas de alteração de política por texto malicioso de PDF/chunk, corpos com identidade e configuração arbitrária; manter conteúdo como dado e garantir segredos fora de logs e referências fabricadas fora dos resultados. Arquivos: `tests/Api.RAG.Tests/Contracts/TransportEnvelopeTests.cs`, `tests/Api.RAG.Tests/Contracts/IngestionContractTests.cs`, `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`, `src/Api.RAG/Tools/KnowledgeMcpTools.cs`, `src/Api.RAG/Application/Knowledge/Ingestao/IngestionMetadata.cs`. Depende de: T047. Rastreio: FR-019; SC-008.
- [ ] T049 [US3] Executar jornadas de ausência/parcial/falha/cancelamento e validar todas as categorias e contagem de tentativas; comprovar nenhuma declaração de conformidade e registrar evidência da classificação de resultados. Arquivos: `tests/Api.RAG.Tests/Integration/KnowledgeLimitationsJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/us3.md`. Depende de: T048, T045. Rastreio: SC-004, SC-008.

**Checkpoint**: validar US3 com os testes e registrar resultado antes de marcá-la concluída.

## Phase 6: User Story 4 - Atualizar e reprocessar sem redundância (Priority: P2)

**Goal**: Preservar repetição segura, gerações e referências no reenvio e na atualização por nova versão (ADR 0009; retirada administrativa, descoberta de artefatos e expurgo removidos).

**Independent Test**: Semear versão ativa e histórica; testar reenvio, falhas, corridas e nova versão; referências antigas resolvem com estado e nenhuma geração incompleta é exposta.

- [ ] T050 [P] [US4] Escrever testes PostgreSQL de ingestão/reprocessamento concorrentes, replays e hash conflitante; mesma identidade/versão com outros bytes sempre rejeitada, não criando revisão interna ou contornando por reprocessamento. Arquivos: `tests/Api.RAG.Tests/Integration/DocumentConcurrencyTests.cs`. Depende de: T049. Rastreio: FR-010–012; SC-002, SC-006. **Atualização 2026-11-10 (ADR 0009):** reprocessamento = reenvio; parte coberta por IngestionContractTests (convergência concorrente) e CompleteRagJourneyTests (reenvio após falha/trechos ausentes).
- [ ] T051 **Cancelada em 2026-11-10 (ADR 0009): reprocess/withdraw/expurge e política de retenção não existem mais.** [P] [US4] Escrever testes de contrato de reprocess/withdraw/expurge e retenção com relógio fake: negar ausência de política, prazo futuro e falta de permissão, não apagar no vencimento, não remover origem ainda ativa e manter referências sem redirecionamento. Arquivos: `tests/Api.RAG.Tests/Contracts/LifecycleContractTests.cs`, `tests/Api.RAG.Tests/Unit/RetentionPolicyTests.cs`. Depende de: T049. Rastreio: FR-012–016; SC-003, SC-005.
- [ ] T052 **Cancelada em 2026-11-10 (ADR 0009): descoberta de artefatos retidos removida (schema excluído).** [P] [US4] Escrever testes do GET administrativo de artefatos retidos conforme contracts/retained-artifacts.schema.json: knowledge.audit central obrigatório, somente metadados, items vazio/versão inexistente, política ausente e revision forte por item; elegibilidade consultada não concede knowledge.expurge e mudança concorrente deve rejeitar a precondição antiga. Arquivos: `tests/Api.RAG.Tests/Contracts/RetainedArtifactDiscoveryTests.cs`. Depende de: T049. Rastreio: FR-014–016; SC-003, SC-005.
- [X] T053 [US4] Modelar RetencaoArtefato com todos os campos previstos, `eventType` (`withdrawn` ou `superseded`) e opcionais `policyVersion?`, `retentionDuration?`, `eligibleForExpurgeAt?`, `expurgedAt?`, `expurgedBy?`, `expurgeReason?`; preservar “Valor ausente não significa zero.” e “Repetição não reinicia marco.”; sem recálculo retroativo/exclusão silenciosa. Arquivos: `src/Api.RAG/Domain/Knowledge/RetencaoArtefato.cs`, `src/Api.RAG/Domain/Knowledge/RetentionDecision.cs`, `src/Api.RAG/Domain/Knowledge/ObrigacaoRetencao.cs`. Depende de: T050, T051. Rastreio: FR-014; P01. **Supersedida em 2026-11-10 (ADR 0009):** RetencaoArtefato/RetentionDecision/ObrigacaoRetencao removidos.
- [X] T054 [US4] Implementar migração/repositório de eventos, versão de política, vencimento e tombstones com própria retenção; impedir expurgo de artefato compartilhado por geração ativa ou outra obrigação ainda vigente, com revisão/lock e auditoria transacional. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetention.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0008_retention.sql`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0009_retention_obligations.sql`. Depende de: T053. Rastreio: FR-014, FR-016; P01. **Supersedida em 2026-11-10 (ADR 0009):** KnowledgeRetention removido; migrações 0008/0009 mantidas sem uso.
- [X] T055 [US4] Implementar consulta por versão documental e projeção de metadados retidos com snapshot consistente: retainedArtifactId UUID, category não vazia, state retained/purged, eventType withdrawn/superseded e eventAt UTC; policyVersion/eligibleForExpurgeAt nulos sem política válida, eligibleForExpurge false com impediments e revision opaca de ETag forte por artefato. Não retornar PDF, texto, embedding ou endereço de armazenamento; estado de elegibilidade não inclui autorização de excluir. Arquivos: `src/Api.RAG/Application/Knowledge/Publicacao/AdministrarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetention.cs`, `src/Api.RAG/Application/Knowledge/Ports/IKnowledgeStore.cs`. Depende de: T054, T052. Rastreio: FR-014–016; SC-003. **Supersedida em 2026-11-10 (ADR 0009):** projeção de artefatos retidos removida.
- [X] T056 [US4] Expor GET /admin/v1/document-versions/{id}/retained-artifacts com knowledge.audit central, DTO versionado e correlationId, items vazio para versão sem artefatos/404 para inexistente, rejeitando acesso sem permissão e nunca expondo conteúdo; revision do item deve ser reutilizável literalmente em If-Match de expurgo e diferente da revisão da lista/versão documental. Arquivos: `src/Api.RAG/Controllers/KnowledgeLifecycleController.cs`, `src/Api.RAG/Application/Knowledge/Ports/IKnowledgeStore.cs`. Depende de: T055, T011, T052. Rastreio: FR-014–016; SC-003, SC-005. **Supersedida em 2026-11-10 (ADR 0009):** GET retained-artifacts removido (404).
- [X] T057 [US4] Implementar pedido de reprocessamento idempotente com perfil versionado, reutilizando fila/worker e origem imutável; sourceHash deve permanecer igual, geração candidata isolada e geração ativa consultável durante processamento/falha. Arquivos: `src/Api.RAG/Application/Knowledge/Publicacao/AdministrarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeLifecycle.cs`. Depende de: T054, T031. Rastreio: FR-010–012. **Supersedida em 2026-11-10 (ADR 0009):** POST reprocess removido; reprocessamento ocorre por reenvio do mesmo documento.
- [X] T058 [US4] Implementar conclusão/troca integral da geração em transação curta com comparação de revisão/estado/ponteiro/perfil, evento superseded e auditoria; sucesso de fonte ainda published ativa sem nova aprovação, retirada concorrente impede reativação e erro não altera ativa. Arquivos: `src/Api.RAG/Domain/Knowledge/VersaoDocumental.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeLifecycle.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRepository.cs`. Depende de: T057. Rastreio: FR-012–013, FR-016. **Supersedida em 2026-11-10 (ADR 0009):** troca de geração mantida, agora na publicação automática (KnowledgePublication), sem revisão/ponteiro esperados.
- [X] T059 [US4] Implementar retirada idempotente com motivo e revisão, marcando withdrawn/evento de retenção e excluindo novas buscas sem apagar evidências; retry não reinicia marco e approve/reprocess não restauram fonte retirada. Arquivos: `src/Api.RAG/Application/Knowledge/Publicacao/AdministrarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeLifecycle.cs`, `src/Api.RAG/Domain/Knowledge/VersaoDocumental.cs`. Depende de: T058. Rastreio: FR-013–014. **Supersedida em 2026-11-10 (ADR 0009):** POST withdraw removido; retirada ocorre somente quando nova versão da fonte é publicada.
- [X] T060 [US4] Implementar expurgo manual por knowledge.expurge central: revalidar política, prazo vencido, revisão e não atividade/dependências no efeito, não por timer; executar exclusão/tombstone/auditoria e negar sem configuração ou antes do prazo. Arquivos: `src/Api.RAG/Application/Knowledge/Publicacao/AdministrarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetention.cs`. Depende de: T059, T054, T011. Rastreio: FR-014–016; P01. **Supersedida em 2026-11-10 (ADR 0009):** expurgo removido.
- [X] T061 [US4] Completar resolução de referência em gerações superseded/withdrawn e tombstones: durante retenção texto exatamente citado, após expurgo indisponibilidade tipada; corrida com expurgo retorna visão consistente ou ausência explícita, nunca texto parcial/novo. Arquivos: `src/Api.RAG/Application/Knowledge/Consulta/ConsultarConhecimento.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetrieval.cs`. Depende de: T060, T040. Rastreio: FR-009, FR-014; SC-003. **Supersedida em 2026-11-10 (ADR 0009):** resolução de superseded/withdrawn mantida; tombstones só existem em dados anteriores.
- [X] T062 [US4] Expor POST reprocess/withdraw/expurge com contratos v1, precondições, motivos e permissões próprias; conectar a aplicação sem regra de domínio em endpoint e sem expor ciclo administrativo em tool MCP. Arquivos: `src/Api.RAG/Controllers/KnowledgeLifecycleController.cs`. Depende de: T061. Rastreio: FR-010–018. **Supersedida em 2026-11-10 (ADR 0009):** endpoints reprocess/withdraw/expurge removidos (404).
- [ ] T063 **Cancelada em 2026-11-10 (ADR 0009): jornada de descoberta/expurgo removida.** [US4] Executar jornada administrativa de descobrir artefato/revisão, conferir ausência de conteúdo, negar leitura sem auditoria e negar expurgo sem permissão/política/prazo; entre leitura elegível e comando alterar revisão, exigir 412 sem exclusão, reler e só então expurgar se ainda autorizado. Incluir lista vazia, versão inexistente e tombstone. Arquivos: `tests/Api.RAG.Tests/Integration/RetainedArtifactDiscoveryJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/retained-artifact-discovery.md`. Depende de: T056, T062, T060, T052. Rastreio: FR-014–016; SC-003, SC-005.
- [X] T064 [US4] Implementar operação técnica controlada de corte do perfil/catalogo com gerações já preparadas para todas as versões publicadas a manter, uma transação consistente e rejeição se faltar geração; não criar endpoint público e preservar referências antigas. Arquivos: `src/Api.RAG/Application/Knowledge/Catalogo/AdotarPerfilDoCatalogo.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/CatalogActivation.cs`. Depende de: T062, T037. Rastreio: FR-006, FR-012, FR-020–021; P02. **Supersedida em 2026-11-10 (ADR 0009):** corte de catálogo (AdotarPerfilDoCatalogo/CatalogActivation) removido; busca usa o perfil configurado.
- [ ] T065 [US4] Executar jornada concorrente completa: duplicação zero, conflito rejeitado, troca sem mistura, falha preservada, retirada durante ativação, resolução/expurgo e origem compartilhada; registrar testes e comportamento antes/depois com política sintética. Arquivos: `tests/Api.RAG.Tests/Integration/LifecycleJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/us4.md`. Depende de: T064, T050, T051, T063. Rastreio: SC-002–003, SC-005–006. **Atualização 2026-11-10 (ADR 0009):** escopo reduzido a duplicação zero, conflito, troca sem mistura, falha preservada e substituição por nova versão; sem retirada administrativa/expurgo.

**Checkpoint**: validar US4 com os testes e registrar resultado antes de marcá-la concluída.

## Phase 7: User Story 5 - Administrar acesso e avaliar qualidade (Priority: P2)

**Goal**: Comprovar a matriz de acesso e produzir avaliação comparativa e medição de capacidade/latência. Desde a ADR 0009 a avaliação documenta parâmetros, mas não condiciona a busca.

**Independent Test**: Matriz de identidades sintéticas demonstra negações/atribuições; harness recusa avaliação sem aprovação e compara candidatos em snapshot isolado. Aceite oficial exige P02/P03, sem inventar corpus.

- [ ] T066 [P] [US5] Escrever testes da matriz completa de permissões/read histórico/admin central vs organizacional, separação de dados operacionais e ação permitida/negada auditada; garantir aprovação pelo autor aceita e nenhuma escrita com permissionamento só de leitura. Arquivos: `tests/Api.RAG.Tests/Contracts/AuthorizationMatrixTests.cs`. Depende de: T065. Rastreio: FR-015–016; SC-005. **Atualização 2026-11-10 (ADR 0009):** retirar "aprovação pelo autor"; não há aprovação.
- [ ] T067 [P] [US5] Escrever testes do protocolo de avaliação: recusar manifesto/expectativas sem aprovação fiscal, relatório vazio e parâmetros não avaliados; impedir candidato publicado, mistura de espaços e cutover incompleto do catálogo. Arquivos: `tests/Api.RAG.Tests/Unit/EvaluationApprovalTests.cs`, `tests/Api.RAG.Tests/Integration/CandidateIsolationTests.cs`. Depende de: T065. Rastreio: FR-020–021; SC-007. **Atualização 2026-11-10 (ADR 0009):** sem cutover de catálogo; manter apenas regras do manifesto/relatório.
- [ ] T068 [US5] Modelar Avaliacao/Manifesto com campos de data-model, `fiscalApprover`, `approvedAt`, `technicalExecutor` obrigatórios no conjunto aprovado e `executedAt?`; preservar “Resultado não executado não pode ser aprovado por preenchimento de relatório vazio.”; registrar manifesto versionado, critérios, hashes, ambiente, amostra e cache. Arquivos: `src/Api.RAG/Domain/Knowledge/Avaliacao.cs`, `src/Api.RAG/Domain/Knowledge/ManifestoAvaliacao.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0010_evaluation.sql`. Depende de: T066, T067. Rastreio: FR-020–021.
- [ ] T069 [US5] Preparar registro de recebimento/aprovação pelo responsável fiscal designado dos PDFs oficiais, versões, aplicabilidade, perguntas, referências esperadas, negativos e critérios; preencher somente com evidência fornecida, manter pendente se ausente e não copiar PDFs/sigilosos ao Git sem política. Arquivos: `specs/001-base-fiscal-versionada/evaluation/manifest.json`, `specs/001-base-fiscal-versionada/evaluation/fiscal-approval.md`. Depende de: T068, T006. Rastreio: P02; D12; FR-020. **Atualização 2026-11-10 (ADR 0009):** continua válida, mas não condiciona a busca.
- [ ] T070 [US5] Implementar harness comparativo de candidatos sobre snapshot staged isolado, medindo evidência esperada/precisão/negativos, custo e duração por perfil; provider real só em execução explicitamente configurada, fixtures técnicas independentes do manifesto oficial. Arquivos: `tests/Api.RAG.Tests/Evaluation/RetrievalEvaluationTests.cs`, `tests/Api.RAG.Tests/Evaluation/EvaluationReportWriter.cs`. Depende de: T068. Rastreio: FR-020–021; SC-007. **Atualização 2026-11-10 (ADR 0009):** continua válida; sem snapshot staged de catálogo.
- [ ] T071 **Cancelada em 2026-11-10 (ADR 0009): adoção de perfil por avaliação deixou de condicionar a busca.** [US5] Implementar validação de adoção de perfil por avaliação executada/aprovada, registrando todos os parâmetros/modelo/dimensão e relatório; nenhum perfil candidate é selecionável pelo consumidor e não aprovar defaults sem comparação; integrar com corte consistente já implementado. Arquivos: `src/Api.RAG/Application/Knowledge/Avaliacao/AprovarPerfilAvaliado.cs`. Depende de: T070, T064. Rastreio: FR-020–021; D04.
- [ ] T072 [US5] Documentar protocolo SC-009/010 antes do ensaio: 300 PDFs com bytes/páginas/trechos/hashes, 5 consultas efetivamente simultâneas, ambiente/provedor/cache/amostra/duração e critério de aprovação; não tratar 300 como limite máximo nem inventar SLA de ingestão. Arquivos: `specs/001-base-fiscal-versionada/evaluation/capacity-protocol.md`. Depende de: T070. Rastreio: P03; SC-009–010.
- [ ] T073 [US5] Implementar ensaio cliente MCP medindo envio até resposta completa, incluindo embedding e transporte, excluindo parecer LLM; ≥95% de todas as consultas em até 5s, falhas/timeouts no denominador/relatório e validação de invariantes em cada resposta. Arquivos: `tests/Api.RAG.Tests/Capacity/KnowledgeCapacityTests.cs`, `tests/Api.RAG.Tests/Capacity/ClientLatencyRecorder.cs`. Depende de: T072, T042. Rastreio: SC-009–010.
- [ ] T074 [US5] Executar matriz completa, fechar eventuais lacunas de autorização na aplicação e auditoria; validar acesso compartilhado às fontes por consumidores de organizações distintas sem tornar logs/consultas públicos ou conceder poder central. Arquivos: `tests/Api.RAG.Tests/Integration/AdministrativeAuthorizationJourneyTests.cs`, `specs/001-base-fiscal-versionada/validation/access-matrix.md`. Depende de: T066, T071. Rastreio: FR-015–016; SC-005.
- [ ] T075 **Cancelada em 2026-11-10 (ADR 0009): sem expurgo, não há prazos P01 a aprovar para exclusão.** [US5] Obter e registrar aprovação de prazos P01 por categoria, originais/referências/tombstones, auditoria/consultas, marco e autoridade de expurgo antes de persistência real; sem definições manter ativação bloqueada e não inventar prazo ou retenção infinita. Arquivos: `specs/001-base-fiscal-versionada/validation/retention-approval.md`. Depende de: T006, T065. Rastreio: P01; FR-014; constitution §4.
- [ ] T076 [US5] Registrar validação P04 do emissor/audience/permissões, limites/timeouts/retry/lease, TLS, segredos e compatibilidade de pacotes em ambiente autorizado, sem publicar valores secretos; diferenciar teste local e validação real, sem realizar deploy como parte desta tarefa. Arquivos: `specs/001-base-fiscal-versionada/validation/environment-readiness.md`. Depende de: T075, T074. Rastreio: P04.
- [ ] T077 [US5] Com P01/P04 comprovados e P02 satisfeito por fiscalmanifest, executar avaliação oficial comparativa antes de fixar chunk/topK/limiar, registrar resultados e escolha fundamentada; se critério falhar ou insumo faltar, registrar bloqueio e não concluir a tarefa nem adotar perfil. Arquivos: `specs/001-base-fiscal-versionada/evaluation/retrieval-report.md`, `specs/001-base-fiscal-versionada/adrs/0002-perfil-avaliado.md`. Depende de: T069, T070, T071, T075, T076. Rastreio: P02; FR-020–021; SC-007. **Atualização 2026-11-10 (ADR 0009):** P01 encerrado; a avaliação documenta parâmetros, sem adoção como gate.
- [ ] T078 [US5] Com P01/P04 comprovados, P02/P03 e perfil adotado, executar ensaio sobre 300 PDFs e 5 consultas simultâneas, registrando percentil e percentual de sucessos completos em até 5s, falhas e ambiente; se necessário medir alternativa/atualizar ADR e repetir ensaio afetado, nunca declarar desempenho presumido. Arquivos: `specs/001-base-fiscal-versionada/evaluation/capacity-report.md`, `specs/001-base-fiscal-versionada/adrs/0001-arquitetura-rag.md`. Depende de: T077, T073. Rastreio: P03; D11; SC-009–010. **Atualização 2026-11-10 (ADR 0009):** P01 encerrado e perfil configurado, sem adoção formal.
- [ ] T079 [US5] Consolidar resultados independentes da história (acesso, avaliação e capacidade), identificando fiscal/executor e gates não satisfeitos; não marcar qualidade/feature pronta com resultado oficial ausente ou falho. Arquivos: `specs/001-base-fiscal-versionada/validation/us5.md`. Depende de: T074, T077, T078. Rastreio: SC-005, SC-007, SC-009–010.

**Checkpoint**: validar US5 com os testes e registrar resultado antes de marcá-la concluída.

## Phase 8: Polish & Cross-Cutting Concerns

**Goal**: Concluir verificações transversais e evidências de entrega, sem implantar ou aprovar produção implicitamente.

**Independent Test**: DoD rastreável, todos os testes aplicáveis executados e gates reais fechados ou entrega explicitamente limitada, sem tarefas bloqueadas marcadas como concluídas.

- [ ] T080 Implementar e executar verificações de dependências/organização: domínio sem SDK/HTTP/banco, aplicação por portas, endpoints sem regras duplicadas, nomes do glossário e análise estática/formatação; documentar refatorações com testes de comportamento. Arquivos: `tests/Api.RAG.Tests/Unit/ArchitectureDependencyTests.cs`, `specs/001-base-fiscal-versionada/validation/code-quality.md`. Depende de: T065, T074, T002. Rastreio: QR-001–003; constitution §2.13–15.
- [ ] T081 Validar reinício do worker com leases, falha entre embeddings/banco, retry de efeitos, timeout/cancelamento e backup/restauração do banco em infraestrutura isolada autorizada; preservar referências, políticas e métricas desconhecidas sem alegar zero. Arquivos: `tests/Api.RAG.Tests/Integration/OperationalRecoveryTests.cs`, `specs/001-base-fiscal-versionada/validation/recovery.md`. Depende de: T075, T018, T065. Rastreio: P01, P04; FR-016, FR-018.
- [ ] T082 Executar build e categorias Unit/Contract/Integration/Evaluation/Capacity conforme o guia, com precondições satisfeitas; atualizar caminhos/comando real de migração, configuração e resultados passou/falhou/ignorado; não executar avaliação sem P02 ou declarar testes não rodados. Arquivos: `specs/001-base-fiscal-versionada/quickstart.md`, `specs/001-base-fiscal-versionada/validation/verification-report.md`. Depende de: T079, T076, T080, T081. Rastreio: P01–P04; constitution DoD.
- [ ] T083 Consolidar matriz FR-001–022, QR-001–003 e SC-001–010 → tarefas → nomes dos testes → evidências/TRX; revisar constitution 1.1.0 e contratos, sinalizar desvios ou falhas sem aprovação fictícia. Arquivos: `specs/001-base-fiscal-versionada/validation/traceability.md`. Depende de: T082. Rastreio: constitution §2.12, §7.
- [X] T085 Simplificar a Api.RAG para ingestão e busca via MCP: remover Idempotency-Key/If-Match, publicar automaticamente ao concluir vetores, retirar versões anteriores da mesma fonte, reaproveitar/refazer geração por reenvio, usar o perfil configurado na busca e remover approve/reprocess/withdraw/retained-artifacts/expurge com o código e testes exclusivos; registrar desvio constitucional. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgePublication.cs`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/DocumentRepository.cs`, `src/Api.RAG/Domain/Knowledge/VersaoDocumental.cs`, `src/Api.RAG/Infrastructure/Configuration/RagOptionsValidator.cs`, `tests/Api.RAG.Tests/Integration/CompleteRagJourneyTests.cs`, `specs/001-base-fiscal-versionada/adrs/0009-simplificacao-ingestao-busca.md`. Depende de: T062. Rastreio: FR-010–014, FR-022; ADR 0009.
- [X] T086 Ambiente local e configuração de desenvolvimento: PostgreSQL/pgvector + pgAdmin em Docker, scripts de setup/ingestão, seção `OpenAI` (chave, modelos, preços no consumo), etapa vetorial ligada pela chave, Swagger em Development e limites para o Guia Prático (433 páginas). Arquivos: `infra/local/docker-compose.yml`, `scripts/local/setup-rag.ps1`, `scripts/local/ingest-sample.ps1`, `src/Api.RAG/Infrastructure/Configuration/RagOptions.cs`, `src/Api.RAG/Endpoints/OpenApi/KnowledgeOpenApi.cs`, `tests/Api.RAG.Tests/Unit/OpenAiConfigurationTests.cs`, `specs/001-base-fiscal-versionada/adrs/0010-ambiente-local-openai-swagger.md`. Depende de: T029, T033. Rastreio: constitution §2.10 (consumo); P04 local.
- [X] T087 Melhorar a relevância da busca: busca híbrida (cosseno + tsvector português, RRF), normalização `repeated-lines-v1`, segmentação `record-sections` com seção observada e sumário excluído, embedding com título da seção, conjunto técnico de avaliação e script de medição; perfil de desenvolvimento v5. Arquivos: `src/Api.RAG/Infrastructure/Knowledge/Persistence/Migrations/0010_lexical_search.sql`, `src/Api.RAG/Infrastructure/Knowledge/Persistence/KnowledgeRetrieval.cs`, `src/Api.RAG/Ingestao/PageNormalizer.cs`, `src/Api.RAG/Ingestao/StructuralSegmenter.cs`, `src/Api.RAG/Ingestao/TokenWindowSegmenter.cs`, `tests/Api.RAG.Tests/Unit/SegmentationQualityTests.cs`, `specs/001-base-fiscal-versionada/evaluation/busca-guia-1.35.json`, `scripts/local/avaliar-busca.ps1`, `specs/001-base-fiscal-versionada/adrs/0011-relevancia-busca-hibrida.md`. Depende de: T085, T086. Rastreio: FR-004–007, FR-017, FR-021 (conjunto técnico, não oficial); não substitui T069/T077.
- [ ] T084 Atualizar ADR/plan/research/guia apenas para refletir decisões realmente implementadas e avaliadas; registrar DoD e riscos remanescentes, sem alterar a constitution, executar deploy ou marcar gates externos aprovados por suposição. Arquivos: `specs/001-base-fiscal-versionada/plan.md`, `specs/001-base-fiscal-versionada/research.md`, `specs/001-base-fiscal-versionada/adrs/0001-arquitetura-rag.md`, `specs/001-base-fiscal-versionada/validation/done.md`. Depende de: T083. Rastreio: QR-003; constitution §7–8.

**Checkpoint**: validar as saídas da fase e as dependências antes do próximo incremento.

## Dependencies & Execution Order

### Phase Dependencies

```text
Setup T001–T006
  └─ Fundação T007–T018
       └─ US1 T019–T034
            └─ US2 T035–T043
                 └─ US3 T044–T049
                      └─ US4 T050–T065
                           └─ US5 T066–T079
                                └─ Fechamento T080–T084 (T085–T087: simplificação, ambiente local e relevância, concluídas)
```

O grafo mostra ordem conservadora de integração. Os predecessores por tarefa são o grafo
efetivo: T075 deve concluir após US4 e antes dos ensaios reais; T076 deve validar o ambiente
antes de T077/T078. T072/T073 podem ser preparados antes dos ensaios e T074 pode
validar acesso enquanto aguarda fontes/ensaios. Nenhuma história precisa repetir ingestão para
cada teste: fixtures podem fornecer versões publicadas, gerações e políticas sintéticas.
Não inferir permissão para paralelizar mutações do mesmo arquivo ou acesso ao ambiente real.

### User Story Dependencies

| História | Pré-requisitos de código | Teste independente e resultado verificável |
| --- | --- | --- |
| US1 — 16 tarefas | Fundação pronta. | PDF sintético ingestível/acompanhável, trecho rastreável e publicação automática somente após vetores. |
| US2 — 9 tarefas | Modelo/persistência de US1; fixture já publicada. | Cliente MCP recupera só versão compatível e resolve texto exato; sem REST funcional ou acesso ao banco pelo consumidor. |
| US3 — 6 tarefas | Contratos e busca de US2. | Base preparada + falhas simuladas distinguem vazio/parcial/contexto/falha e não fabricam conformidade. |
| US4 — 16 tarefas (3 canceladas) | Ingestão/busca/falhas estáveis. | Corridas, reenvio e nova versão mantêm origem/referências sem gerações incompletas expostas. |
| US5 — 14 tarefas (2 canceladas) | Jornadas anteriores estáveis. | Matriz de acesso e avaliação sintética testáveis isoladamente; aceite oficial e carga dependem de P02/P03 e resultados reais. |

### Within Each User Story

Testes de contrato e invariantes precedem a implementação correspondente. Modelos precedem
persistência/casos de uso; casos de uso precedem endpoints; integração executada precede
checkpoint. Reutilizar portas e regras de domínio; endpoints não duplicam elegibilidade ou
retenção. Program.cs e arquivos compartilhados devem ser integrados de modo sequencial.
Revisar dependências, nomenclatura, erros explícitos e análise estática a cada incremento.

### Parallel Opportunities

Há seis lotes conservadores em arquivos distintos: cinco pares e um trio, após concluir os predecessores comuns:

- Setup: T002 e T003, depois de T001.
- US1: T019 e T020, depois da fundação.
- US2: T035 e T036, depois de US1.
- US3: T044 e T045, depois de US2.
- US4: T050, T051 e T052, depois de US3.
- US5: T066 e T067, depois de US4.

Não há paralelismo automático de produção entre histórias. É possível preparar evidências de
políticas, manifestos e capacidade por responsáveis humanos em paralelo ao código, respeitando
os gates, sem marcar os ensaios correspondentes como realizados.

## Parallel Example: User Story 1

```text
T019: tests/Api.RAG.Tests/Contracts/IngestionContractTests.cs
T020: tests/Api.RAG.Tests/Unit/DocumentInvariantsTests.cs e Fixtures/Documents/SyntheticPdfFactory.cs
Pré-condição: T018 concluída; saídas diferentes; não editar modelo/host compartilhado neste lote.
```

## Parallel Example: User Story 2

```text
T035: tests/Api.RAG.Tests/Contracts/McpSchemaTests.cs
T036: tests/Api.RAG.Tests/Integration/VersionEligibilityTests.cs
Pré-condição: T034 concluída; usar fixture pronta; não depender de ingestão durante o teste.
```

## Parallel Example: User Story 3

```text
T044: tests/Api.RAG.Tests/Contracts/KnowledgeOutcomeTests.cs
T045: tests/Api.RAG.Tests/Integration/RecoveryFailureTests.cs
Pré-condição: T043 concluída; mocks de dependência restritos ao cenário técnico.
```

## Parallel Example: User Story 4

```text
T050: tests/Api.RAG.Tests/Integration/DocumentConcurrencyTests.cs
T051: tests/Api.RAG.Tests/Contracts/LifecycleContractTests.cs e Unit/RetentionPolicyTests.cs
T052: tests/Api.RAG.Tests/Contracts/RetainedArtifactDiscoveryTests.cs
Pré-condição: T049 concluída; banco/relógio isolados por teste; não compartilhar dados mutáveis.
```

## Parallel Example: User Story 5

```text
T066: tests/Api.RAG.Tests/Contracts/AuthorizationMatrixTests.cs
T067: tests/Api.RAG.Tests/Unit/EvaluationApprovalTests.cs e Integration/CandidateIsolationTests.cs
Pré-condição: T065 concluída; usar fixture e manifesto sintéticos, sem presumir aprovação fiscal.
```

## Matriz de cobertura para revisão

| Requisito/critério | Tarefas principais | Evidência de validação |
| --- | --- | --- |
| FR-001–003 | T019–T023, T026–T034 | Jornada US1, recusa de formatos/metadados e estados. |
| FR-004, FR-009 | T024, T026–T029, T035–T043, T061 | Localização observada/ausente e resolução exata por MCP. |
| FR-005–006 | T022, T035–T043, T064 | Filtro determinístico de versão, perfil e geração, SC-001. |
| FR-007–008 | T044–T049 | Categorias e falhas distintas, SC-004. |
| FR-010–011 | T022, T030, T050, T065, T085 | Repetição segura por identidade do documento, conflito de bytes rejeitado, SC-002/006. |
| FR-012–014 | T023, T058, T061, T065, T085 | Troca consistente, publicação automática, substituição por nova versão e referências, SC-003/006. |
| FR-015–016 | T011, T016–T018, T033–T034, T066, T074, T076 | Matriz/auditoria e papel central, SC-005. |
| FR-017 | T018, T035, T041–T043 | Cliente real MCP sem REST funcional, SC-008. |
| FR-018–019 | T010, T027, T030–T031, T044–T049 | Falhas/correlação e dados não confiáveis, SC-004/008. |
| FR-022 | T027–T029, T038–T043 | Embeddings de documentos/consulta, espaço compatível e busca semântica via MCP. |
| FR-020–021 | T025, T028–T029, T067–T070, T077 | Manifesto aprovado e comparação documentada (sem gate de adoção), SC-007. |
| ADR 0009/0010 | T085, T086 | Simplificação, ambiente local, OpenAI e Swagger; 97 testes. |
| ADR 0011 | T087 | Busca híbrida e segmentação por registro; hit@5 93%, MRR 0,844 em conjunto técnico; 100 testes. |
| QR-001–003 | T002–T003, T012, T021–T025, T080, T083–T084 | DDD, organização, análise estática e ADR revisável. |
| SC-009–010 | T072–T078, T082 | 300 PDFs, 5 consultas simultâneas, ≥95% completas em até 5s pelo cliente. |
| Consumo constitucional | T016–T017, T029, T045, T081 | Todas as tentativas, métricas nulas e custo estimado separado. |

A matriz referencia intervalos de apoio; o detalhamento por teste/artefato executado será
completado em T083, sem trocar números de requisito, critérios ou evidências por suposição.

## Implementation Strategy

### MVP First (User Story 1 Only)

Concluir setup, fundação e US1: um incremento administrativo demonstrável com PDF/perfil
sintéticos, processamento rastreável e publicação automática. Este MVP não fornece ainda todo
o fluxo de recuperação, avaliação oficial ou aceite produtivo da feature.
Para primeiro incremento funcional ao consumidor, adicionar US2 e US3 com cenários negativos.

### Incremental Delivery

1. US1 fecha ingestão e publicação automática.
2. US2 entrega recuperação MCP e referências com elegibilidade determinística.
3. US3 fecha limitações, falhas e conteúdo não confiável.
4. US4 entrega reenvio seguro, concorrência e substituição por nova versão.
5. US5 comprova autorização, avaliação e capacidade.
6. Fechamento executa guia, revisão e matriz/DoD com resultados reais.

Preparar testes e mecanismos com dados sintéticos antes de receber corpus e políticas reais;
não ativar operação dependente sem esses insumos. Não abrir exceção constitucional para
contornar uma tarefa bloqueada por responsável externo.

### Parallel Team Strategy

Os lotes [P] são oportunidades de trabalho, não instrução para criar agentes ou realizar
delegação nesta execução. Nenhum código foi implementado por este comando. Dentro de cada
lote, manter os arquivos isolados e integrar os resultados antes das tarefas dependentes.

## Notes

- Total: **87 tarefas** — Setup 6, Fundação 12, US1 16, US2 9, US3 6, US4 16, US5 14, Fechamento 8 (T085/T086 acrescentadas em 2026-11-10; T087 em 2026-11-11).
- Estado atual: 60 concluídas, 22 abertas e 5 canceladas (T051, T052, T063, T071, T075). Tasks *Supersedidas* continuam concluídas como histórico; o comportamento vigente está na ADR 0009. Não transformar obtenção de aprovação em aprovação presumida.
- Quantidades/limiares não fornecidos permanecem externos e avaliáveis, nunca defaults aprovados.
- Scripts/comandos efetivamente adotados devem ser atualizados no quickstart durante implementação.
- P01–P04 podem bloquear tarefas específicas e aceite; não alegar feature concluída com elas abertas.

## Histórico — ajustes 1–4 (2026-11-05)

Organização, recebimento administrativo, modelos/persistência e preparação textual implementados. T020–T028 e T030 concluídas neste escopo. T019/T031/T033/T034 parcialmente implementadas: faltam os critérios integrais de embeddings/aprovação; suas caixas continuam abertas. Referencia é UUID imutável na linha de Trecho, sem entidade redundante. Fila é o estágio persistido da geração, sem broker ou fila em memória.

O worker textual termina em preparationState=ready, operação queued/EMBEDDINGS_PENDING e publicação unpublished. Dependência de T029 para conclusão integral de US1 preservada; a antecipação do recebimento textual foi autorizada no escopo 1–4 e registrada no [ADR 0005](adrs/0005-ingestao-textual-incremental.md). Evidência: [60 testes aprovados](validation/ingestion-adjustments.md). Nenhuma tool funcional, embedding real ou aprovação foi anunciada como pronta.

## Histórico — correções dos ajustes 1–4 (2026-11-05)

- [X] Limitar tentativas de preparação de modo durável e persistir falhas técnicas/esgotamento.
- [X] Retornar identidade, ETag e cronologia integrais da operação canônica em reingestões.
- [X] Validar perfis completos e unicidade por UUID na inicialização.
- [X] Distinguir erros conhecidos de PDF de falhas técnicas inesperadas.
- [X] Aplicar casos de uso autorizados nas leituras e transições de ciclo de vida no domínio.

Evidência daquele incremento: [68 testes aprovados](validation/ingestion-review.md). Configuração externa
agora requer Processing.MaxPreparationAttempts. Migrações existentes preservadas; T029,
T032 e os critérios integrais de T019/T031/T033/T034 continuam pendentes conforme o escopo anterior.

## Histórico — ajustes 5–11 (2026-11-05)

Implementados SDK OpenAI medido, worker vetorial com checkpoints/lease, manifesto integral, PgVector exato, aprovação, reprocessamento, retirada, retenção/expurgo e tools MCP com schemas explícitos. Casos de uso/arquivos coesos substituem classes finas planejadas sem duplicar SQL ou regras. Recuperação usa ConsultarConhecimento + ConhecimentoService + KnowledgeRetrieval; administração usa AdministrarConhecimento + KnowledgeLifecycle/KnowledgeRetention; resultados usam EvidenceOutcome + ContractValidator. Os handlers MCP são alternativa oficial ao registro por atributos e mantêm o catálogo estrito.

Evidência daquele incremento: [83 testes aprovados](validation/rag-complete.md). O caso técnico de corte de catálogo foi implementado e exercitado no SQL com perfil sintético; o domínio recusa conjuntos incompletos. Tasks de matriz/corridas integrais, harness/avaliação oficial, capacidade, backup/restauração e gates reais continuam abertas onde o aceite não está comprovado. As declarações anteriores de embeddings/tools pendentes são históricas dos incrementos anteriores. Api.Agente permanece feature 002 sem implementação.

T053 inclui RetencaoArtefato, duração explícita em dias, política, marco, vencimento, ator/motivo de expurgo e revisão, sem recálculo retroativo. Arquivos concretos estão mapeados em validation/rag-complete.md; classes finas planejadas foram consolidadas sem duplicação de regras.

Correções da revisão de RG-009/RG-010: ADR 0007 registra geração de outro perfil staged,
obrigações de retenção independentes no domínio e preservação de uso conhecido em vetor
inválido. A migração 0009 é de retenção; a migração planejada de avaliação passa a 0010.
Os gates fiscais, de avaliação e de capacidade continuam pendentes.

Evidências das quatro correções: [94 testes aprovados](validation/review-corrections.md).

## Histórico — simplificação e ambiente local (2026-11-10)

T085/T086 concluídas. ADR 0010: ambiente Docker com pgAdmin, seção `OpenAI` com preços
gravados no consumo, etapa vetorial ligada pela chave, Swagger em Development e limites para PDFs
grandes (Guia Prático 1.35: 433 páginas, 814 trechos). ADR 0009: sem Idempotency-Key/If-Match,
publicação automática, nova versão retira as anteriores, reenvio refaz geração com falha ou sem
trechos, busca com o perfil configurado e remoção de approve/reprocess/withdraw/retained-artifacts/
expurge, catálogo e retenção ativa. Desvio constitucional definitivo registrado. Evidência: 97 testes.

## Histórico — relevância da busca (2026-11-11)

T087 concluída. ADR 0011: busca híbrida (migração 0010), perfil de desenvolvimento v5
(`repeated-lines-v1`, `record-sections`) e consulta da Api.Agente no vocabulário do guia.
Conjunto técnico de 15 consultas sobre o Guia 1.35: hit@5 57% → 93%, MRR 0,332 → 0,844.
Evidência: Api.RAG 100 testes, Api.Agente 130 testes. T069/T077 continuam pendentes.
