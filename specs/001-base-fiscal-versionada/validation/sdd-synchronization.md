# Sincronização dos artefatos SDD

Data: 2026-11-10 (revisão após a simplificação da ADR 0009 e o ambiente local da ADR 0010).
Escopo: documentação de toda a Api.RAG e consistência das features 002/003 com ela.
Não houve alteração da constitution, commit, push ou deploy.

## Estado e evidência de referência

| Feature | Estado registrado |
| --- | --- |
| 001 — base fiscal / Api.RAG | 86 tasks: 59 concluídas, 22 abertas e 5 canceladas (ADR 0009). Ingestão com publicação automática, vetorização, consulta MCP, ambiente local, seção OpenAI e Swagger implementados. |
| 002 — validação SPED / Api.Agente | 17 tasks: 8 concluídas e 9 abertas (estado do tasks.md da feature; código e testes em `src/Api.Agente` e `tests/Api.Agente.Tests`). Consome as tools MCP da Api.RAG sem mudança de contrato. |
| 003 — integração Sofia A2A | 10 tasks abertas; somente documentação. |
| Última suíte Api.RAG | 97 aprovados, 0 falhas em 2026-11-10; build sem avisos/erros; `dotnet format` exit 0. |
| Operação real | P02/P03/P04 pendentes; P01 encerrado pela ADR 0009 (sem expurgo). Ingestão real executada apenas no ambiente local de desenvolvimento. |

## Implementação e cobertura disponíveis (mapa parcial de T083)

| Requisitos / tasks | Implementação atual | Evidência existente e limite |
| --- | --- | --- |
| RG-003; FR-001–004; T019–T034 | Controllers/IngestController; Application/Knowledge/Ingestao; Ingestao/PdfTextExtractor e segmentadores; DocumentRepository e workers textual/vetorial. | IngestionContractTests.ConcurrentUploadsConvergeAndTextOnlyRemainsUnpublished; IngestionJourneyTests (lease, PDF parcial, reenvio após esgotamento). Corpus sintético. |
| RG-004; FR-005–006; T035–T043 | ConsultarConhecimento, ConhecimentoService, KnowledgeRetrieval; perfil configurado (RagOptionsValidator.OperationalProfile). | CompleteRagJourneyTests.SimilarTextInDifferentSourcesRemainsIsolatedByLayout. Sem avaliação oficial de relevância/capacidade. |
| RG-005; FR-017; T041–T043 | Tools/KnowledgeMcpTools e tools tipadas; Fixtures/McpConsumerProcess. | McpSchemaTests e jornadas REST/Kestrel/MCP com consumidor separado. |
| RG-008; FR-007–008/018; T046–T048 | EvidenceOutcome, ConsultarConhecimento, ConhecimentoService, ContractValidator, MeasuredEmbeddingProvider. | CompleteRagJourneyTests.ProviderFailureIsMeasuredAndResubmissionRetries; FoundationTests; TransportEnvelopeTests. T044/T045/T049 abertas. |
| FR-010–014/016; T058/T061/T085 | KnowledgePublication (publicação automática, substituição de geração, retirada de versões anteriores), DocumentRepository (reaproveitamento/refação por reenvio), VersaoDocumental.PublishGeneration. | CompleteRagJourneyTests: IngestionPublishesAutomaticallyAndNewVersionReplacesPrevious, ResubmissionAfterChunksWereDeletedReprocesses, SavedVectorsSurviveLeaseRecoveryAndCompletionPublishes, LifecycleEndpointsWereRemoved; VectorDomainTests.PublicationRequiresCompletedGenerationOfSameVersion. |
| FR-015–016; T011/T017/T048 | KnowledgeAuthorization, AuthenticatedActor, DevelopmentAuthenticationHandler, auditoria de transporte. | FoundationTests, DevelopmentAuthenticationTests, IngestionContractTests.UploadRequiresCentralPermission. Matriz integral T066/T074 aberta. |
| Consumo; T016/T029/T086 | MeasuredEmbeddingProvider, ConsumptionRepository, ConsumptionMeasurement; preço de `OpenAI:Pricing`. | ConsumptionMeasurementTests, OpenAiConfigurationTests, EmbeddingConsumptionCorrectionsTests. |
| Ambiente local; T086 | infra/local (Docker + pgAdmin), scripts/local, seção OpenAI, Swagger. | OpenAiConfigurationTests; ingestão real do Guia Prático 1.35 (814 trechos). |
| QR-001–003 | Domínio, portas e adaptadores; analisadores existentes. | Build/formatação e testes de invariantes (parcial). T080/T081/T083 abertas. |
| RG-001/002/006/007 | Feature 002 (Api.Agente): parser SPED, cliente MCP e diagnósticos implementados parcialmente; parecer LLM pendente. | Ver specs/002-validacao-sped-parecer. |

## Decisões confirmadas

- [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md): sem Idempotency-Key/If-Match;
  publicação automática; versão mais recente retira as anteriores; reenvio reaproveita ou refaz
  geração; busca com perfil configurado; approve/reprocess/withdraw/retained-artifacts/expurge,
  catálogo e retenção ativa removidos. Desvio constitucional definitivo, responsável Vitor Vianna.
- [ADR 0010](../adrs/0010-ambiente-local-openai-swagger.md): Docker com pgAdmin, seção `OpenAI`
  (gpt-4.1-mini para a Api.Agente, text-embedding-3-small, preços), etapa vetorial ligada pela
  chave, Swagger em Development e limites locais ampliados.
- [ADR 0008](../adrs/0008-configuracao-local-modelos.md): dispensa de JWT só em Development
  loopback (mantida); escolha de gpt-4o-mini e etapa vetorial desligada substituídas pela ADR 0010.
- Migrações 0001–0009 inalteradas; tabelas sem uso preservadas por compatibilidade.

## Pendências

- T044/T045/T049: matriz completa de resultados, falhas, timeout e cancelamento.
- T050/T065: concorrência de reenvio e substituição por nova versão.
- T066–T070/T072–T074: permissões, avaliação/harness e protocolo/ensaio de capacidade.
- T069/T076/T077–T079: insumos fiscais, ambiente real e relatórios oficiais.
- T080–T084: arquitetura, recuperação/backup, rastreio e DoD final.

## Verificação desta edição

Os links Markdown locais dos artefatos alterados foram verificados; o build e a suíte da
Api.RAG foram reexecutados após as mudanças de código desta data (97/97).
