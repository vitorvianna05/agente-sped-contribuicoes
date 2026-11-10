# Evidências — ajustes 5 a 11

> **Nota (2026-11-10):** relatório histórico. O ciclo administrativo e o catálogo aqui evidenciados foram removidos pela [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md).

Data: 2026-11-05. Escopo: API RAG, sem implementação da Api.Agente e sem deploy.

| Ajuste | Entrega | Evidência |
| --- | --- | --- |
| 5 — Embeddings | SDK OpenAI real, modelo/dimensão externos, chave externa, retries SDK desativados e decorator medido por tentativa | OpenAiEmbeddingTests (payload/usage e 429 sem retries ocultos); testes existentes de medição/retries/moedas |
| 6 — Vetores | PgVector, perfil imutável, manifesto integral, checkpoints e lease cercado por proprietário | CompleteRagJourneyTests.SavedVectorsSurviveLeaseRecoveryAndWithdrawalFencesActivation; VectorDomainTests |
| 7 — Busca | SQL exato/cosseno, filtros relacionais, perfil/catalogo em snapshot e confronto do perfil persistido antes de gerar embedding de consulta | CompleteRagJourneyTests.SimilarTextInDifferentVersionsRemainsIsolated; jornada completa com ausência antes de approve |
| 8 — Ciclo de vida | Approve/reprocess/withdraw, retenção descoberta por metadados, expurgo por revisão/prazo/dependências e corte técnico integral de catálogo | Jornada completa de REST/MCP; retirada durante ativação; VectorDomainTests de publicação/retenção/catalogo |
| 9 — MCP | BuscarConhecimento e ResolverReferencia no SDK stateless com autorização por chamada e schemas completos | Consumidor real separado em todas as jornadas; McpSchemaTests; negação de extras antes de chamada ao provider |
| 10 — Contratos | structuredContent/texto JSON equivalentes, validação semântica, referências exatas e falhas tipadas | ContractValidator em produção e consumidor; tombstone, versão incompatível, perfil ausente e provider indisponível |
| 11 — Testes | Regressões Unit/Contract/Integration, HTTP simulado OpenAI, HTTP/Kestrel real, PostgreSQL isolado e processo MCP independente | TRX sanitizado e comandos abaixo; corpus e parâmetros exclusivamente sintéticos |

## Execução

Suíte completa: **83 aprovados, 0 falhas, 0 ignorados** (34 Unit, 16 Contract,
33 Integration), incluindo as 68 regressões anteriores.
Execução usa PostgreSQL/PgVector descartável e aplica as oito migrações duas vezes com identidades
de migrador/aplicação separadas. Não há chamadas externas faturáveis; SDK OpenAI usa transporte
HTTP simulado nos testes unitários. Os testes antigos de preparação textual desativam somente
o worker vetorial no host sintético; as jornadas completas habilitam ambas as etapas.
TRX: [rag-complete.trx](rag-complete.trx).

Build final: 0 avisos e 0 erros. Formatação final verificada sem alterações (exit 0).

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/rag-accepted
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

## Limites de aceite

Os ajustes funcionais 5–11 estão implementados. As caixas de tasks que exigem ensaios fiscais,
matriz/concorrência integral ou infraestrutura real continuam abertas quando a evidência ainda
não cobre todo seu aceite. O corte técnico de catálogo foi exercitado com perfil sintético no SQL e tem testes de completude; não foi executado um corte de perfil real. Backup/restauração, avaliação oficial comparativa e ensaio 300 PDFs/5
consultas não foram realizados. Nenhum preço, modelo final, chunk/topK/limiar ou regra fiscal foi
aprovado por convenção. Secrets e aprovação operacional devem ser fornecidos externamente.

As migrações 0001–0006 foram preservadas. 0007/0008 são aditivas e não foram aplicadas a banco real.
O parser permanece cooperativo entre páginas. Não houve commit, push, cobrança OpenAI ou deploy.
