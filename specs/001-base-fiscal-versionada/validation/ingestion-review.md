# Correções da revisão dos ajustes 1–4

> **Nota (2026-11-10):** relatório histórico; o reenvio agora segue a [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md) (sem chave de idempotência).

Data: 2026-11-05. Escopo: cinco achados relacionados a requirements.md e architecture.md.

| Achado | Correção | Evidência |
| --- | --- | --- |
| Job em repetição indefinida | Processing.MaxPreparationAttempts externo, positivo e obrigatório; contador durável limita retomadas; esgotamento falha e audita atomicamente. Exceções de job recebem desfecho TECHNICAL_FAILURE; shutdown conserva lease e timeout permanece separado. | IngestionReviewTests.DomainBoundsPreparationAttemptsAndRejectsExpiredOwners; IngestionJourneyTests.ExhaustionIsDurableAndReingestionHasCanonicalChronology; IngestionContractTests.WorkerRecordsTechnicalFailureAndReadUseCaseRequiresPermission |
| Cronologia híbrida de reingestão | Operação, Location e ETag são canônicos; chaves adicionais preservam idempotência sem combinar createdAt novo com execução antiga. | IngestionJourneyTests.ExhaustionIsDurableAndReingestionHasCanonicalChronology, inclusive consulta pelo UUID interno adicional |
| Perfis incompletos aceitos na inicialização | Todos os perfis passam pela validação completa; IDs são UUIDs e unicidade é por valor, independentemente de caixa/formato; novo limite de tentativas é obrigatório. | IngestionReviewTests.StartupRejectsIncompleteProcessingConfiguration (3 casos) e StartupRejectsEquivalentProfileIdentifiers |
| Erro técnico convertido em PDF ilegível | Adaptador traduz somente exceções conhecidas de formato/encriptação do PdfPig. I/O e erros inesperados são propagados e classificados como falha técnica pelo worker. | IngestionReviewTests.PdfAdapterPreservesTechnicalFailures; regressões de PDF corrompido/parcial mantidas; teste HTTP do worker com falha de I/O sintética |
| Camadas e domínio ignorados | Controllers delegam ao ConsultarIngestao; autorização knowledge.audit também ocorre no caso de uso. Claim/CompletePreparation e Start/Finish do domínio governam as transições, inclusive retomada e esgotamento; SQL apenas grava os valores validados. | Testes de ciclo de vida sem HTTP/banco; integração de lease e tentativa; acesso direto ao caso de uso negado para principal sem atribuição central |

## Validação

Suíte completa: **68 aprovados, 0 falhas e 0 ignorados** (28 Unit, 15 Contract,
25 Integration), incluindo os 60 testes anteriores. PDF sintético, PostgreSQL/PgVector
descartável, autenticação sintética e consumidor MCP existente. Nenhuma chamada OpenAI.
TRX sanitizado: [ingestion-review.trx](ingestion-review.trx).

Build final aprovado com 0 avisos e 0 erros. Formatação verificada sem alterações (exit 0).

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/ingestion-review
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

## Compatibilidade e limites

Não houve alteração de migrações existentes nem migração de ambiente real.
Atualizar configuração externa com Processing.MaxPreparationAttempts antes de iniciar o host.
Perfil configurado incompleto agora impede startup, conforme o contrato de configuração.
Operações adicionais da mesma geração resolvem para a identidade original, mantendo os campos v1.
Indisponibilidade do banco impede confirmar uma falha; após recuperação, o limite persistido
controla a retomada. Não se alega gravação de um desfecho enquanto o banco está indisponível.

O cancelamento do parser continua cooperativo entre páginas. Embeddings, publicação,
recuperação semântica e parecer continuam pendentes; a feature completa não foi aprovada.
Sem commit ou push neste incremento.
