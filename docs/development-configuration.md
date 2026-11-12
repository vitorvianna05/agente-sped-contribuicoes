# Configuração local da Api.RAG

Guia do ambiente de desenvolvimento. Fluxo vigente: **ingerir PDF → publicação automática →
buscar via MCP**, sem `Idempotency-Key`, `If-Match` nem aprovação manual
([ADR 0009](../specs/001-base-fiscal-versionada/adrs/0009-simplificacao-ingestao-busca.md)).
Decisões deste ambiente: [ADR 0010](../specs/001-base-fiscal-versionada/adrs/0010-ambiente-local-openai-swagger.md).

## Início rápido

Executar na raiz do repositório, com Docker ativo:

```powershell
./scripts/local/setup-rag.ps1
dotnet run --project src/Api.RAG --launch-profile Api.RAG
./scripts/local/ingest-sample.ps1 -SkipCertificateCheck
```

1. `setup-rag.ps1` (pode ser repetido): copia `appsettings.Development.Example.json` para
   `appsettings.Development.json` se este não existir, gera `infra/local/.env` com senhas
   aleatórias, sobe os containers, grava `ConnectionStrings:Rag` e `ConnectionStrings:RagMigration`
   nos User Secrets e aplica as 9 migrações.
2. O perfil `Api.RAG` roda em Development em https://localhost:60994 e abre `/swagger`.
3. `ingest-sample.ps1` envia um PDF sintético (ou `-PdfPath <arquivo>`, com `-OfficialIdentifier`
   e `-OfficialVersion`) e acompanha a operação até concluir.

## Arquivos de configuração

`appsettings.Example.json` é um template incompleto que o ASP.NET Core não carrega. Em
Development são carregados `appsettings.Development.json` (ignorado pelo Git) e os User Secrets
do projeto (UserSecretsId no csproj); variáveis de ambiente e argumentos sobrescrevem ambos.
Não copiar `synthetic-settings.json`: o provider Fake é exclusivo dos testes. O validador de
startup lista as chaves ausentes/inválidas sem imprimir segredos (`OptionsValidationException`).

| Grupo | Configurar |
| --- | --- |
| Limites | `Processing:MaxFileBytes`, `MaxPages`, `MaxConcurrentJobs`, `TimeoutSeconds`, `LeaseSeconds` (> TimeoutSeconds), `MaxPreparationAttempts`; `Retrieval:MaxQueryCharacters`, `TimeoutSeconds`; `Embeddings:TimeoutSeconds`, `MaxAttempts`, `RetryDelayMilliseconds` (≥ 0). |
| Identidade | Modo local: `Authentication:DisabledForDevelopment=true`. Com JWT: `Authority` (emissor HTTPS real), `Audience`, `Resource` (URL HTTPS terminada em /mcp), `AllowedHosts`, `RequireHttpsMetadata=true`, `CentralPermissionAssignments`. |
| Perfil | `Processing:Profiles` com ao menos um perfil completo; `Retrieval:AdoptedProfileId` opcional quando há um único perfil. |
| Embeddings / OpenAI | `Embeddings:Provider` OpenAI, `Dimensions` compatível com o perfil, `SdkAutomaticRetriesEnabled=false`; seção `OpenAI` (abaixo). |
| Retenção | `Retention:PolicyVersion`, `ApprovalReference` e prazos positivos são exigidos para ingerir, mas apenas registrados: a API não expurga nada. |

Sem efeito desde a ADR 0009: `Processing:ProfileApprovalReference`, a seção `Evaluation` e os
campos `AdoptionState`/`EvaluationId` do perfil (este último só tem o formato validado).

## Api.Agente junto com a Api.RAG

```powershell
./scripts/local/setup-agente.ps1                 # depois do setup-rag.ps1
dotnet user-secrets set Llm:ApiKey <chave> --project src/Api.Agente
dotnet run --project src/Api.Agente --launch-profile Api.Agente   # https://localhost:60996/swagger
```

`setup-agente.ps1` (pode ser repetido) cria no mesmo container o banco `agente` com os papéis
`agente_migration` (proprietário) e `agente_application`, acrescenta as senhas a
`infra/local/.env`, grava `AgentData:ConnectionString`/`MigrationConnectionString` nos User
Secrets da Api.Agente, copia `appsettings.Development.Example.json` se o arquivo local não
existir (ignorado pelo Git) e aplica `--migrate-agent`.

O exemplo Development da Api.Agente traz, apenas para desenvolvimento:

- `AgentData`: política `local-dev-v1` / `LOCAL-DEV-NOT-APPROVED`, auditoria e consumo de 30 dias.
- `LayoutPolicy`: perfil **provisório** COD_VER `006`, 2020-01-01 a 2026-12-31 →
  `EFD-Contribuicoes` / `1.35`, igual à aplicabilidade do Guia Prático na RAG. Os códigos e
  períodos cobertos pelo guia 1.35 ainda precisam de decisão fiscal; trocar quando definidos.
- `Limits:TimeoutSeconds` 120: a análise faz busca, até 5 resoluções MCP e até 2 tentativas do LLM.
- `Cors:AllowedOrigins` vazio: sem CORS (a SOFIA backend não precisa). Para um frontend no
  navegador, listar a origem exata (`http://localhost:4200` em Development; HTTPS nos demais).
  `Transport:TrustedProxies`/`TrustedNetworks` vazios: `X-Forwarded-*` ignorados; preencher só
  com o proxy que termina TLS (feature 003, ADR 0002).

Publicação de demonstração (Vercel/Render/Neon/Auth0): feature 004 em
`specs/004-publicacao-cloud/` (Dockerfiles, variáveis em `deploy/`, CI e roteiro). A Api.RAG
também aceita `Transport:TrustedProxies`/`TrustedNetworks`, vazios no exemplo local.
- `Llm:Pricing`: tabela pública do gpt-4.1-mini (US$ 0,40 input / 0,10 cache / 1,60 output por
  milhão). Cada tentativa grava o custo estimado; `GET /api/v1/analises/{id}/consumo` mostra
  tokens (inclusive em cache), custo, referência e versão da tabela (ADR 0004 da feature 002).
  Sem preços, o custo fica nulo. Arquivo local anterior a essa seção: copiar o bloco do exemplo.

Depois de atualizar o código, reaplicar as migrações da Api.Agente
(`dotnet run --project src/Api.Agente -- --migrate-agent` ou `setup-agente.ps1`); sem isso a
tentativa não é finalizada e a análise termina `failed`.

A Api.Agente chama a RAG em `https://localhost:60994/mcp` sem token (loopback local). Isso exige
que o certificado servido pelo Kestrel seja confiável: se houver mais de um certificado de
desenvolvimento, o Kestrel usa o mais novo, que pode não ser o confiável, e a análise falha com
`KNOWLEDGE_UNAVAILABLE` sem que a RAG registre requisição. Correção (altera a confiança do sistema):
`dotnet dev-certs https --clean` e depois `dotnet dev-certs https --trust`.

## Ambiente Docker (PostgreSQL + pgAdmin)

`infra/local/docker-compose.yml` sobe PostgreSQL com pgvector (mesma imagem dos testes, porta
127.0.0.1:5433) e pgAdmin (http://localhost:5050, sem login). `initdb/01-roles.sh` cria a extensão
`vector` e o papel `rag_application`; `rag_migration` é o proprietário usado nas migrações.

No pgAdmin, o servidor **Api.RAG (local)** aparece no grupo *Agente SPED*; a senha pedida é
`RAG_MIGRATION_PASSWORD` de `infra/local/.env`. A Query Tool no banco `rag` com
`infra/local/pgadmin/acompanhar-ingestao.sql` mostra operações, gerações, trechos e auditoria.
Para recomeçar do zero: `docker compose -f infra/local/docker-compose.yml down -v` e rodar o setup.

O exemplo Development traz o perfil `candidate` `d0c0a1e0-0000-4000-8000-000000000005`
(`repeated-lines-v1`, `record-sections` 400/50, text-embedding-3-small/1536; ADR 0011) e retenção `local-dev-v1`/
`LOCAL-DEV-NOT-APPROVED`. São valores de desenvolvimento, não perfil avaliado nem política aprovada.

## Ingestão, reenvio e busca

- Sem `OpenAI:ApiKey`, a ingestão termina em `preparationState=ready`/`EMBEDDINGS_PENDING`: texto
  em `rag.chunks`, sem vetores e sem publicação.
- Com a chave, após os vetores a operação fica `succeeded` e a versão `published`. Uma nova
  `officialVersion` da mesma fonte retira as versões anteriores da busca.
- Reenviar o mesmo documento (mesmos metadados e bytes): devolve a operação existente se a
  geração está concluída ou em andamento; refaz se só houver falhas ou se os trechos não existirem
  mais. Não é preciso apagar nada no banco; apagar trechos à mão quebra referências já devolvidas.
- Bytes diferentes com a mesma versão → 409 `VERSION_CONTENT_CONFLICT`; outra origem/título para
  o mesmo identificador → 409 `SOURCE_METADATA_CONFLICT`.
- A busca (`BuscarConhecimento`) exige `tipoDocumento` e `versaoLayout` iguais a uma
  aplicabilidade do documento (ex.: `EFD-Contribuicoes` / `1.35`). Sem `Threshold` não há corte
  de similaridade; sem `TopK`, até 5 trechos. A busca é híbrida (cosseno + texto em português,
  fundidos por RRF): a ordem é a da fusão e `score` continua sendo o cosseno (ADR 0011).
- Relevância: `./scripts/local/avaliar-busca.ps1 -Detalhar` roda as consultas de
  `specs/001-base-fiscal-versionada/evaluation/busca-guia-1.35.json` no MCP local e mostra
  hit@k/MRR (exige o Guia 1.35 ingerido; conjunto técnico, não aprovado).

## Modo local de autenticação

`Authentication:DisabledForDevelopment=true` dispensa JWT somente em Development e para conexões
loopback. A auditoria identifica o ator como `development-local`, com todas as permissões locais;
cabeçalhos do cliente não definem essa identidade. Em outros ambientes a flag causa erro de
startup. Metadados OAuth retornam 404 nesse modo. HTTPS e a lista de hosts continuam exigidos.

## Swagger (somente Development)

Interface em https://localhost:60994/swagger; documento OpenAPI 3.1 em
https://localhost:60994/openapi/v1.json. Fora de Development nada é registrado.

- No modo local as chamadas saem autenticadas como `development-local`; com JWT, usar **Authorize**.
- `Authentication:AllowedOrigins` precisa conter `https://localhost:60994`: o navegador envia
  `Origin` nos POSTs e o TransportGuard recusa origens fora da lista (403).
- POST `/api/v1/ingest`: escolher o PDF em `arquivo`; `metadata` vem preenchido com um exemplo
  (ajustar `officialIdentifier`/`officialVersion` ao documento). Não há cabeçalhos obrigatórios.
- Os GET `/admin/v1/operations/{id}` e `/admin/v1/document-versions/{id}` acompanham o processamento.
- A busca não aparece no Swagger: é exposta somente via MCP em `/mcp`.
- Se o navegador recusar o certificado: `dotnet dev-certs https --trust`.

## Seção OpenAI

| Chave | Uso na Api.RAG |
| --- | --- |
| `OpenAI:ApiKey` | Chave do cliente de embeddings. Prevalece `Embeddings:ApiKey` se também definida; valores diferentes falham no startup. |
| `OpenAI:EmbeddingModel` | Preenche `Embeddings:Model` (`text-embedding-3-small`, 1536 dimensões). Deve coincidir com `EmbeddingModel` do perfil. |
| `OpenAI:ChatModel` | `gpt-4.1-mini`. Apenas validado: o parecer pertence à Api.Agente; a Api.RAG não chama esse modelo. |
| `OpenAI:Pricing` | `Currency` obrigatória quando há preços; valores não negativos por milhão de tokens. `Embedding:InputPerMillionTokens` gera o custo estimado em `rag.consumption_attempts` (`price_reference`/`price_version`; versão derivada dos valores quando `Version` é nulo). |

`Processing:EmbeddingStageEnabled` aceita `null` (padrão): a etapa vetorial liga sozinha quando a
chave existe e fica inativa, com aviso no log, quando está vazia. `true` sem chave falha no
startup; `false` desliga mesmo com chave. Preencher a chave por um destes meios (o último
carregado prevalece):

- `"OpenAI": { "ApiKey": "<chave>" }` em `src/Api.RAG/appsettings.Development.json` (ignorado pelo Git);
- `dotnet user-secrets set OpenAI:ApiKey <chave> --project src/Api.RAG` (fora da pasta do projeto);
- variável de ambiente `OpenAI__ApiKey`. Não há leitura automática de `OPENAI_API_KEY`.

## Limites para PDFs grandes

Cada trecho gera uma chamada de embedding e a geração inteira precisa caber em
`Processing:TimeoutSeconds`; estourar o prazo falha a geração (`OPERATION_TIMEOUT`) e o reenvio a
refaz. O exemplo Development usa 50 MB, 1000 páginas, timeout de 900 s e lease de 960 s. Medição
de referência: Guia Prático da EFD-Contribuições 1.35 (433 páginas, 3,9 MB) → 814 trechos,
~467 mil tokens, US$ 0,0093 e ~4,5 min (~3 trechos/s). Acima de ~2.500 trechos, aumentar o
timeout (e o lease). O mesmo valor limita o tempo das requisições HTTP.

Não há substituição por vetores falsos. Modelos: [embeddings](https://developers.openai.com/api/docs/models/text-embedding-3-small)
e [GPT-4.1 mini](https://developers.openai.com/api/docs/models/gpt-4.1-mini).
Guia das funcionalidades e cenários: [quickstart](../specs/001-base-fiscal-versionada/quickstart.md).
