# ADR 0004 — Custo estimado do parecer por tabela configurada

Data: 2026-11-11. Status: aceito. Responsável: Vitor Vianna. Complementa a ADR 0003 (consumo por
tentativa); substitui nela apenas a ausência de cálculo tarifário. Tabela de desenvolvimento
não é tabela aprovada para faturamento.

## Contexto

O ledger já registrava tokens de cada tentativa do modelo gerador, mas `estimated_cost` ficava
sempre NULL: não havia tabela de preços. A Api.RAG grava o custo dos embeddings desde a ADR 0010
da feature 001, então o consumo da análise ficava incompleto apenas do lado da Api.Agente.

## Decisão

- Seção `Llm:Pricing`: `Currency`, `Reference`, `Version`, `InputPerMillionTokens`,
  `CachedInputPerMillionTokens` e `OutputPerMillionTokens`. Sem nenhum preço → custo NULL (como
  antes). Com algum preço, `Currency`, input e output são obrigatórios e nenhum valor pode ser
  negativo; caso contrário o startup falha. Cache sem preço próprio usa o preço do input.
- `Version` ausente é derivada dos valores (`cfg-` + 12 hex do SHA-256), como na Api.RAG.
  `Reference` ausente vira `configuration:Llm:Pricing`.
- O adaptador OpenAI lê `usage.prompt_tokens_details.cached_tokens` além de `prompt_tokens` e
  `completion_tokens`. Migração `0002_llm_cached_tokens` acrescenta
  `agente.consumption_attempts.cached_input_tokens` (≥ 0 e ≤ input).
- Custo = ((input − cache) × preço input + cache × preço cache + output × preço output) / 10⁶,
  gravado em `estimated_cost`/`estimated_currency` com `price_reference`/`price_version`.
  Input ou output desconhecido, ou cache maior que o input → custo NULL, nunca zero. Cache não
  informado conta como input integral (estimativa superior).
- `provider_reported_cost`/`currency` continuam NULL: a OpenAI não informa custo na resposta.
- O exemplo Development usa os preços públicos do gpt-4.1-mini (US$ 0,40 / 0,10 / 1,60 por
  milhão, mesma tabela da seção `OpenAI:Pricing` da Api.RAG); o exemplo de produção traz tudo
  NULL. Embeddings continuam medidos só pela RAG; nada é somado entre as APIs.

## Consequências

- `GET /api/v1/analises/{id}/consumo` passa a trazer `cachedInputTokens` e o custo estimado;
  `unknownCostAttempts` cai a zero quando há tabela e o provedor informa o uso.
- Bancos existentes precisam de `--migrate-agent` (o `setup-agente.ps1` já executa). Sem a
  migração, finalizar a tentativa falha e a análise termina `failed` com a tentativa `pending`.
- Mudança de preço exige atualizar a configuração; tentativas antigas mantêm a versão com que
  foram calculadas.

## Evidência

`LlmPricingTests` (8 casos), cache no `OpenAiParecerTests`, custo persistido e lido no
PostgreSQL em `ConsumptionTests`; suíte Api.Agente 138/138. Execução real local: 7.317 tokens de
input (7.168 em cache) + 331 de output → US$ 0,001306, `unknownCostAttempts=0`.
