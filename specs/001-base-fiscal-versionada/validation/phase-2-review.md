# Correções da revisão — Phase 2

Data: 2026-11-05. Escopo: quatro achados da revisão de princípios; T011, T014 e T016.

| Achado | Correção | Evidência |
| --- | --- | --- |
| Término anterior à criação com startedAt nulo | Operacao valida FinishedAt >= CreatedAt; migration 0005 adiciona a mesma proteção ao banco. | ReviewRegressionTests.CancellationWithoutStartCannotFinishBeforeCreation e FoundationTests.DatabaseRejectsFinishBeforeCreationWithoutStart. |
| Moeda inferida da estimativa | currency preserva somente informação do provedor; estimated_currency separada, nullable e aditiva. Moedas diferentes são persistidas sem conversão. | ConsumptionMeasurementTests e FoundationTests.ConsumptionPersistsIndependentCurrencies (moeda ausente e diferente). |
| Regra monetária dentro do repositório | ConsumptionPrice e ConsumptionMeasurement no domínio validam preço/métricas e calculam em decimal; decorator cria a medição, repositório somente mapeia/persiste. | Testes unitários sem banco para cálculo, valores negativos, referência ausente, tokens/preço ausentes e zero explícito. |
| AuthenticatedActor sem uso | Validação JWT, autorização e auditoria de transporte usam TryFrom; From mantém contrato estrito. Identidade não autenticada não fornece principal/organização. | ReviewRegressionTests.ActorRequiresAuthenticatedSubject, incluindo principal com identidades mistas; regressão de autenticação/autorização/auditoria integrada. |

## Validação

O teste novo de cronologia foi executado antes da correção e falhou porque nenhuma exceção
era lançada. Após as correções, suíte completa: **41 aprovados, 0 falhas, 0 ignorados**
(16 Unit, 15 Contract, 10 Integration). Inclui regressões anteriores, PostgreSQL/PgVector
descartável, JWT sintético e consumidor MCP real em processo separado.

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/Phase2-Review-Final
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

Build: 0 avisos e 0 erros. Formatação verificada e aprovada (exit 0). TRX sanitizado: [phase-2-review.trx](phase-2-review.trx).
Os 32 testes de phase-2.md são a evidência histórica anterior às correções; o aceite atual
é o desta revisão. Não houve alteração de preços reais, credenciais ou aprovação operacional.

## Compatibilidade e escopo

Não se alteraram os contratos públicos REST/MCP v1. O schema interno evolui por migração
aditiva, preservando os checksums das quatro anteriores. O comando --migrate aplica as cinco
migrações no ambiente isolado e pode ser repetido. Não foi executada migração de produção.
Nenhuma moeda histórica é reclassificada por inferência ou conversão automática.

Os arquivos futuros de migração previstos em T026/T037/T054/T068 foram renumerados para
0006–0009, evitando colisão com 0005_foundation_review; tarefas permanecem abertas e sem
mudança de escopo. Modelo de dados e ADR-0003 refletem os novos campos/responsabilidades.
Gates P01–P04 continuam pendentes para operação real.
