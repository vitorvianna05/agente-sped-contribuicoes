# ADR 0010 — Ambiente local, seção OpenAI e Swagger

Data: 2026-11-10. Status: aceito. Substitui em ADR 0008 a escolha de `gpt-4o-mini`, a chave em
`Embeddings:ApiKey` como origem única e `EmbeddingStageEnabled=false` fixo no exemplo local.
Restrito a desenvolvimento; não aprova configuração produtiva.

## Ambiente local (PostgreSQL + pgAdmin)

- `infra/local/docker-compose.yml`: PostgreSQL com pgvector (mesma imagem com digest fixo dos
  testes) em `127.0.0.1:5433` e pgAdmin em `http://localhost:5050` (modo desktop, sem login).
- `infra/local/initdb/01-roles.sh` cria a extensão `vector` e o papel `rag_application`, distinto
  de `rag_migration` (proprietário/migração), como exige o `MigrationRunner`.
- `scripts/local/setup-rag.ps1` (idempotente): gera `infra/local/.env` com senhas aleatórias
  (ignorado pelo Git), sobe os containers, grava `ConnectionStrings:Rag` e
  `ConnectionStrings:RagMigration` nos User Secrets e aplica as migrações.
- `scripts/local/ingest-sample.ps1`: envia um PDF (sintético por padrão) e acompanha a operação.
- `infra/local/pgadmin/servers.json` pré-cadastra o servidor; `acompanhar-ingestao.sql` traz
  consultas de operações, gerações, trechos e auditoria.
- `.gitattributes` força LF em `*.sh`, executados no container Linux.

## Seção `OpenAI`

```json
"OpenAI": { "ApiKey": "", "ChatModel": "gpt-4.1-mini", "EmbeddingModel": "text-embedding-3-small",
  "Pricing": { "Currency": "USD", "Reference": "...", "Version": null,
    "Chat": { "InputPerMillionTokens": 0.4, "CachedInputPerMillionTokens": 0.1, "OutputPerMillionTokens": 1.6 },
    "Embedding": { "InputPerMillionTokens": 0.02 } } }
```

- `OpenAI:ApiKey` e `OpenAI:EmbeddingModel` preenchem `Embeddings:ApiKey`/`Embeddings:Model`
  quando estes estão vazios (`RagOptions.ApplyOpenAiSection`, via PostConfigure). Valores
  diferentes nas duas seções falham no startup.
- `Processing:EmbeddingStageEnabled` passou a `bool?`: `null` liga a etapa vetorial somente
  quando há chave; `true` sem chave falha no startup; `false` desliga.
- `Pricing.Embedding.InputPerMillionTokens` + `Currency` geram `ConsumptionPrice`, gravado em
  `rag.consumption_attempts` (`estimated_cost`, `price_reference`, `price_version`). Sem `Version`,
  a versão é derivada dos valores (`cfg-` + hash). Preços negativos ou sem moeda falham no startup.
  Antes desta decisão o preço nunca era repassado e o custo estimado ficava sempre nulo.
- `ChatModel` e preços de chat são apenas validados: a Api.RAG não chama modelos de chat
  (responsabilidade da Api.Agente).
- A chave não é lida de `OPENAI_API_KEY`; use `OpenAI:ApiKey` em `appsettings.Development.json`
  (ignorado pelo Git), User Secrets ou `OpenAI__ApiKey`.

## Swagger (Development)

`Microsoft.AspNetCore.OpenApi` 10.0.14 gera `/openapi/v1.json` (OpenAPI 3.1) e
`Swashbuckle.AspNetCore.SwaggerUI` 10.3.1 serve `/swagger`, registrados só em Development e
sujeitos ao `TransportGuard` (HTTPS, AllowedHosts). Como os controllers leem corpo e cabeçalhos
manualmente, um transformador descreve o upload multipart (`arquivo` + `metadata` pré-preenchido).
Os controllers usam `[ApiExplorerSettings(IgnoreApi = false)]` em vez de `[ApiController]`, que
mudaria validação/binding. `Authentication:AllowedOrigins` em Development inclui
`https://localhost:60994`, pois o navegador envia `Origin` nos POSTs. Com JWT, o documento expõe
esquema Bearer. `launchSettings` abre `/swagger`.

## Limites locais

O exemplo Development usa `MaxFileBytes` 50 MB, `MaxPages` 1000, `TimeoutSeconds` 900 e
`LeaseSeconds` 960. Medição de referência: Guia Prático da EFD-Contribuições 1.35 (433 páginas,
3,9 MB) gerou 814 trechos, ~467 mil tokens de embedding, US$ 0,0093 e ~4,5 min (~3 trechos/s).
Estourar `TimeoutSeconds` falha a geração (`OPERATION_TIMEOUT`); o mesmo valor limita requisições
HTTP. Perfil de desenvolvimento `d0c0a1e0-0000-4000-8000-000000000001` (token-window 400/50) e
retenção `local-dev-v1`/`LOCAL-DEV-NOT-APPROVED` não representam parâmetros avaliados.

## Evidência

Testes `OpenAiConfigurationTests` (7) e suíte completa da Api.RAG; ingestão real local do Guia
Prático com 814 trechos vetorizados e custo registrado; documento OpenAPI com as operações e o
upload descritos; requisição com `Origin` permitido → 202 e origem externa → 403.
