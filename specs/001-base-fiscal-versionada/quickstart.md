# Quickstart — Api.RAG (ingestão e busca via MCP)

Sincronização: 2026-11-10. Api.RAG oferece ingestão com publicação automática, vetorização e busca
MCP ([ADR 0009](adrs/0009-simplificacao-ingestao-busca.md)). Última execução: 97 testes aprovados,
build sem avisos/erros e formatação conferida; ingestão real local do Guia Prático da
EFD-Contribuições 1.35 (814 trechos). Qualidade fiscal, capacidade e ambiente real não estão
aprovados. A Api.Agente (consumidora MCP) é a Feature 002.

## Pré-requisitos

- SDK selecionado por global.json (10.0.303) e pacotes do repositório.
- Docker ativo: ambiente local (PostgreSQL/pgvector + pgAdmin) e testes de integração.
- Não reutilizar configuração Fake de testes em Development ou produção. Chaves e conexões
  ficam em User Secrets/ambiente/`appsettings.Development.json` (ignorado pelo Git), nunca no chat.

## Ambiente local em quatro passos

Detalhes: [configuração local](../../docs/development-configuration.md) e
[ADR 0010](adrs/0010-ambiente-local-openai-swagger.md).

```powershell
./scripts/local/setup-rag.ps1                       # containers, User Secrets e migrações
dotnet run --project src/Api.RAG --launch-profile Api.RAG
./scripts/local/ingest-sample.ps1 -SkipCertificateCheck   # PDF sintético; -PdfPath para um real
```

1. `setup-rag.ps1` gera `infra/local/.env`, sobe PostgreSQL (127.0.0.1:5433) e pgAdmin
   (http://localhost:5050), grava `ConnectionStrings:Rag`/`RagMigration` e aplica as 9 migrações.
2. O perfil Api.RAG roda em Development (https://localhost:60994) e abre `/swagger`.
3. Preencher `OpenAI:ApiKey` liga a etapa vetorial; sem chave a ingestão para em
   `ready`/`EMBEDDINGS_PENDING` e o documento não entra na busca.
4. Concluídos texto e vetores, o documento é publicado e pode ser consultado pelas tools MCP.

`Authentication:DisabledForDevelopment=true` vale somente em Development e conexões loopback
(ator `development-local`); HTTPS/hosts continuam exigidos ([ADR 0008](adrs/0008-configuracao-local-modelos.md)).

## Configuração

| Grupo | Conteúdo |
| --- | --- |
| ConnectionStrings | Rag e RagMigration; identidades distintas para aplicação e migração. |
| Authentication | JWT/emissor/audience/recurso HTTPS, hosts/origens e atribuições centrais no modo normal; em Development, `AllowedOrigins` inclui `https://localhost:60994` (Swagger). |
| Processing | Limites (bytes, páginas, concorrência, timeout, lease, tentativas), `EmbeddingStageEnabled` (null = liga com a chave) e `Profiles`. |
| Retrieval | `AdoptedProfileId` (opcional se houver um único perfil), tamanho e timeout da consulta. |
| Embeddings / OpenAI | Provider, dimensões, timeout e tentativas; `OpenAI:ApiKey`, `EmbeddingModel`, `ChatModel` e `Pricing`. |
| Retention | Exigida para ingerir (versão, referência e prazos positivos), apenas registrada: não há expurgo. |

A chave não é lida de `OPENAI_API_KEY`. `Processing:ProfileApprovalReference` e a seção
`Evaluation` não têm efeito. Campos desconhecidos permanecem nulos, nunca zero.

## Comandos de validação técnica

Executar da raiz, com Docker ativo para a suíte completa. Os testes não fazem chamadas OpenAI
faturáveis: o transporte é simulado e os dados são sintéticos.

```powershell
dotnet restore AgenteSpedContrib.slnx --locked-mode
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --filter-trait Category=Unit
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/current-validation
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

Se a API estiver rodando, o executável fica bloqueado: pare-a antes de compilar ou use
`--artifacts-path` com uma pasta separada.

## Migração e jornada

Migração explícita (as duas conexões configuradas): `dotnet run --project src/Api.RAG --no-launch-profile -- --migrate`.
As nove migrações são aplicadas sem alteração; tabelas de catálogo/retenção ficam sem uso.

Fluxo: POST `/api/v1/ingest` (multipart `arquivo` + `metadata`) → acompanhar `location` até
`succeeded` → versão `published` → `BuscarConhecimento` / `ResolverReferencia` em `/mcp`
com `tipoDocumento` e `versaoLayout` iguais a uma aplicabilidade do documento.
Para refazer um documento, reenviá-lo: geração com falha ou sem trechos é refeita; geração
concluída é reaproveitada. Não apagar trechos à mão.

## Cenários ponta a ponta e resultados esperados

| Cenário | Ações | Resultado |
| --- | --- | --- |
| Ingestão e publicação | POST /api/v1/ingest; acompanhar; buscar antes e depois dos vetores. | Antes dos vetores nenhum trecho ativo; depois, `published` e referências resolvíveis. |
| Sem chave OpenAI | Ingerir sem `OpenAI:ApiKey`. | Operação para em `ready`/`EMBEDDINGS_PENDING`; versão `unpublished`. |
| Formatos | Enviar schema, imagem/PDF sem texto ou conteúdo inválido. | Recusa (415/422) ou operação failed, sem evidência ativa. |
| Repetição | Reenviar o mesmo documento, inclusive simultaneamente. | Mesma operação devolvida; nenhuma operação órfã; nenhuma redundância. |
| Conflito | Bytes diferentes sob mesma identidade/versão. | 409 VERSION_CONTENT_CONFLICT; nada alterado. |
| Reenvio após falha | Falha do provedor, depois reenvio. | Nova operação/geração; ao concluir, publicada. |
| Nova versão | Ingerir outra `officialVersion` da mesma fonte. | Nova versão `published`; anterior `withdrawn`; referências antigas resolvem com estado. |
| Aplicabilidade | Consultar layout compatível e incompatível. | Nenhum trecho incompatível. |
| Falta de evidências | Caso negativo e dependência indisponível. | `insufficient_evidence` no primeiro; `technical_failure`/isError no segundo. |
| Rotas removidas | Chamar approve/reprocess/withdraw/retained-artifacts/expurge. | 404. |
| Segurança | Consumidor tenta ingerir; documento pede ignorar filtros. | Acesso negado; conteúdo não altera política ou filtros. |
| Consumo | Embeddings com retry, timeout e métricas ausentes; preço configurado. | Cada tentativa registrada; custo estimado com `OpenAI:Pricing`; nulos explícitos. |

## Avaliação e capacidade (pendentes)

A avaliação comparativa (T069/T070/T077) documenta parâmetros, mas não condiciona a busca
(ADR 0009). O ensaio SC-009/010 (T072/T073/T078) exige 300 PDFs caracterizados e 5 consultas
concorrentes, ≥95% completas em até 5s medidas no cliente MCP, falhas no denominador.

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Evaluation" --report-xunit-trx
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --filter-trait "Category=Capacity" --report-xunit-trx
```

Essas categorias ainda não têm testes implementados. Não afirmar desempenho de fixtures
pequenas como cumprimento da meta.

## Histórico

Os relatórios Phase 1/2, ajustes 1–4 e [rag-complete.md](validation/rag-complete.md) descrevem
estados anteriores (incluindo o ciclo administrativo removido). Inventário e pendências em
[sdd-synchronization.md](validation/sdd-synchronization.md) e nas tasks abertas.
