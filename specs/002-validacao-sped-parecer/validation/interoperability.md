# Evidência — fluxo estrutural, MCP, parecer e auditoria da Api.Agente

Data: 2026-11-10. Escopo: AG005/006 (subconjunto), AG009/010 e implementação parametrizada
de AG011/012/013. Nenhum arquivo da Api.RAG, dos testes dela ou da Feature 001 foi alterado
por este incremento. Alterações concorrentes daquele serviço não são evidência desta entrega.

## Resultado técnico

- 125 testes aprovados, zero falhas e zero ignorados na suíte Api.Agente.Tests.
- Build da Api.Agente/testes sem avisos/erros e formatação verificada nos dois projetos.
- PostgreSQL próprio descartável em Docker, papéis de migração/aplicação distintos e migração
  idempotente com checksum. Não se aplicou migração ou expurgo em banco real do usuário/RAG.
- Servidor MCP sintético em outro processo, SDK MCP real 2.3.0 via Streamable HTTP.
- OpenAI 2.14.0 real com HTTP simulado; gpt-4.1-mini padrão configurável confirmado pelo usuário.
  Sem chave real, chamada faturável ou avaliação de prosa do modelo neste incremento.

## Rastreabilidade

| Requisito / task | Evidência executada |
| --- | --- |
| FR-AG-001/008/009; AG003/004/012 | AnalysisContractTests, AgentSecurityTests, AnalysisJourneyTests: envelope publicado, IDs do sistema, JWT/scope, proprietário/organização, mídia/charset/limite inclusive chunked, no-store, falha explícita de política ausente. |
| FR-AG-002; AG005/006 parcial | SpedParserTests, SpedReadingTests, SpedValidationTests: lexical/envelope/contagem, campos preservados, datas inválidas/invertidas, período fora de 2020–2026, perfil ausente/sem aprovação/ambíguo. Regras de período/contexto em SpedFileValidation/LayoutApplicability, sem IOptions/HTTP/SDK/SQL no domínio. |
| FR-AG-003/004; AG007/008/012 | KnowledgeClientTests e jornada separada: descoberta/schema/snapshot, bearer no transporte, limites/cancelamento/falhas, contexto obrigatório; consulta montada com resultados mínimos, sem arquivo completo. Não há fallback REST, ProjectReference ou SQL da RAG. |
| FR-AG-004/006; AG009/012 | Referência exata resolvida antes do modelo, comparação completa exceto score; texto/origem/estado retirado divergentes e referência inventada bloqueiam geração/publicação. KnowledgeCalls preserva IDs/correlação remota e persiste vínculo, inclusive technical_failure com resposta válida. |
| FR-AG-005/006; AG009/010 | ParecerValidationTests, OpenAiParecerTests: schema estrito enviado pelo SDK, JSON malformado/duplicado/extra/nulo, referências desconhecidas, tentativa de alterar checks/revisão ou ocultar pendências rejeitadas. Chunks adversariais permanecem dados; resultados do sistema preservados. Robustez de modelo real depende de AG014. |
| FR-AG-007; AG011 parcial | AnalysisPipelineTests: tentativas finitas, JSON inválido seguido de sucesso, esgotamento, timeout, cancelamento, recusa e incompleto; uso conhecido mantido inclusive falha, sem zero fabricado. Intenção pending antes da chamada; falha na gravação impede repetir chamada faturável. OpenAiParecerTests: 429 sem retry oculto, uso ausente/objeto vazio permanece nulo, recusa/truncamento preservam uso reportado. |
| FR-AG-007/009; AG011 parcial | ConsumptionTests + jornada PostgreSQL: duas tentativas independentes, tokens conhecidos/ausentes, custos/moedas desconhecidos nulos, isolamento de subject/organization_id, expiração, expurgo em lote/ordem correta/idempotência, preservação de retenção vigente e DDL negado ao papel HTTP. GET consumo expõe lacunas e agregação conhecida por modelo. |
| SC-AG-001–004; AG012/013 parcial | AnalysisJourneyTests: REST/Kestrel → MCP separado → parecer sintético → consumo real próprio, retry medido e referências vinculadas. Negativos do arquivo, ausência de perfil, erro remoto MCP, referência errada, JSON inválido e recusa não viram conformidade. |
| SC-AG-001–004; AG013 (2026-11-11) | CrossApiJourneyTests: Api.Agente → **Api.RAG real** em outro processo (modo `--rag-server` de Api.RAG.Tests: Kestrel, JWT de serviço, PostgreSQL/PgVector, busca híbrida, contrato 1.0) → gerador sintético. Layout conhecido: evidências reais, cada referência resolvida e idêntica, parecer cita só referências recebidas, consumo no PostgreSQL próprio e 404 para outro sujeito. Negativos: layout sem documentos → partial/`NO_ELIGIBLE_EVIDENCE` sem LLM; falha de embeddings na RAG → failed/`KNOWLEDGE_REMOTE_FAILURE` com trace `technical_failure`; token recusado pela RAG → failed/`KNOWLEDGE_ACCESS_DENIED`; recusa (1 tentativa) e JSON inválido (2 tentativas) do gerador → failed. Nenhum caso devolve parecer aceito ou conformidade. |

## Reprodução

```powershell
dotnet restore tests/Api.Agente.Tests/Api.Agente.Tests.csproj --locked-mode
dotnet build tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore
dotnet test --project tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/agente-delivery-final
dotnet format src/Api.Agente/Api.Agente.csproj --verify-no-changes --no-restore
dotnet format tests/Api.Agente.Tests/Api.Agente.Tests.csproj --verify-no-changes --no-restore
```

TRX final local: TestResults/agente-delivery-final (ignorado pelo Git); resultados anteriores
em agente-analysis-final/agente-mcp-audit-verified/agente-delivery são históricos, com 122 testes.
Docker disponível na execução; bancos/processos de fixture são descartados ao concluir.

Execução final: `INFIS001125_INFISPC0064_2026-11-10_15_39_29.766454.trx`, 125/125 em
39,645 segundos. SHA-256 do TRX:
`29c5867e6fd04add0b54eb85e7a3e8fd7a575acf22a4f1244ee895070f4b1d10`.
Formatação dos dois projetos: exit code 0, sem alterações necessárias.

## Limites e próximos gates

- O usuário deseja 2020–2026; COD_VER não é ano. Não há mapeamento oficial/aprovação para os
  anos solicitados nem tipoDocumento definido. Perfil 006/synthetic-* é apenas fixture.
- Prazos/referência de política e conexão própria serão fornecidos/configurados pelo usuário.
  Exemplos reais ficam sem perfis e sem política; análise não chama MCP/IA sem auditoria.
- Campos por registro, hierarquia de blocos e regras fiscais integrais não estão implementados.
  Resultado atual é partial/failed/unsupported, nunca conformidade/completed; revisão humana
  é obrigatória. Leitura simples pode funcionar sem política de persistência.
- Custo estimado vem de `Llm:Pricing` (ADR 0004); sem tabela, NULL. Faturamento do provedor
  é NULL; não se atribui consumo de embeddings da RAG à Api.Agente.
- AG013 executado contra a Api.RAG real (2026-11-11), com embeddings e gerador sintéticos;
  T049 da feature 001 concluído (validation/us3.md). Referência inválida só é exercitada pelo
  servidor MCP sintético (a RAG real não fabrica referências). A chamada com OpenAI real foi
  verificada manualmente em ambiente local (ADR 0004), fora da suíte automatizada. Não se
  declara SLA ou interoperabilidade produtiva.
- Corpus/rubrica/revisores, avaliação real do modelo e custo/latência integral são AG014.
  Compatibilidade técnica com Structured Outputs não comprova correção tributária da prosa.
- Feature 003 A2A/SOFIA, memória e configuração produtiva não foram implementadas neste escopo.

Estado das caixas: [tasks.md](../tasks.md); decisões: [ADR 0003](../adrs/0003-fluxo-parecer-auditoria.md);
configuração: [quickstart.md](../quickstart.md); política pendente: [data-policy.md](data-policy.md).
