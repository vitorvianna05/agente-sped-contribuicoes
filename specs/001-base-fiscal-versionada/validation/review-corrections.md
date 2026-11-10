# Validação das correções de revisão

> **Nota (2026-11-10):** relatório histórico (94 testes). Correções de catálogo/retenção substituídas pela [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md); a suíte atual tem 97 testes.

Data: 2026-11-10. Escopo: quatro achados RG-009/RG-010, sem deploy ou chamada OpenAI real.

Build da solução: 0 avisos e 0 erros. Formatação final verificada, exit 0. Suíte final: **94 aprovados, 0 falhas, 0 ignorados**
(suíte Unit/Contract/Integration). PostgreSQL/PgVector descartável aplica as nove
migrações duas vezes, com identidades distintas de migrador/aplicação.

| Achado | Regressão |
| --- | --- |
| Outro perfil não ficava staged para corte | LifecycleCorrectionsTests.NewProfileStagesUntilCompleteCutoverAndWithdrawalProtectsSupersededEvidence: duas versões publicadas, rejeição de corte incompleto, busca anterior preservada e corte integral para outro modelo. |
| Retirada perdia obrigação adicional | Mesma jornada: prazo anterior vencido, retirada acrescenta prazo, ETag anterior retorna 412, expurgo prematuro retorna 409, retry preserva marcos/revisão e expurgo só após ambos os prazos. |
| Vetor inválido apagava métricas | FoundationTests.InvalidEmbeddingPreservesReportedUsage: dimensão incorreta e NaN mantêm tokens, custo reportado, estimativa e status failed; exatamente uma tentativa. |
| Regras de prazo na infraestrutura | VectorDomainTests.RetentionObligationsPreserveAllEventDeadlines: cálculo por categoria, maior prazo, pendência e política divergente bloqueiam elegibilidade sem depender de banco/HTTP. |

A primeira execução teve 58 sucessos e 36 falhas de preparação por Docker parado. Após iniciar
Docker, 86 passaram e 8 falharam por disputa da porta 5000 entre hosts de teste. A fixture
passou a usar loopback com porta efêmera; a execução completa final acima passou.

```powershell
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/review-corrections-isolated
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

TRX local: `TestResults/review-corrections-isolated/INFIS001125_INFISPC0064_2026-11-10_12_25_54.931288.trx` (não versionado; pode conter caminhos e logs do ambiente).
Migração nova: 0009_retention_obligations.sql. Nenhuma migração anterior alterada e nenhum
banco real migrado. Backfill sem snapshot comprovado de retirada mantém prazo desconhecido,
conforme ADR 0007. Avaliação fiscal, capacidade e gates de operação real permanecem pendentes.
