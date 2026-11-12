# Plano — publicação cloud de demonstração

Data: 2026-11-12. Alterações mínimas; contratos REST/MCP, rotas e esquema de banco inalterados.

## Matriz de compatibilidade

| Item | Situação | Detalhe |
| --- | --- | --- |
| Hosts | OK | `src/Api.Agente/Api.Agente.csproj` → `Api.Agente.dll`; `src/Api.RAG/Api.RAG.csproj` → `Api.RAG.dll`; net10.0. |
| SDK | Adaptação | `global.json` fixa 10.0.303 (`latestPatch`); a imagem `sdk:10.0` traz 10.0.404. O Dockerfile não copia `global.json`; o alvo continua net10.0 e o CI usa o SDK do `global.json`. |
| Recurso fora do projeto | Adaptação | Api.RAG embute `specs/001-.../contracts/mcp-tools.schema.json`; liberado no `.dockerignore` e copiado no Dockerfile. |
| Api.RAG em serverless | **Bloqueio** | `IngestionWorker` e `EmbeddingWorker` (BackgroundService, leases no banco) e ingestão de ~7 min. → Render Free (processo ativo; dorme após 15 min sem tráfego e os workers param junto). |
| Api.Agente em serverless | Adaptação | Stateless por requisição (análise ~6–15 s). Corpo da Function ~4,5 MB → `Limits:MaxRequestBytes` 4 MB. Container Runtime está em beta: conferir limites de duração/memória no dia. |
| TLS | Adaptação | Ambas exigiam HTTPS no Kestrel. Agente já aceitava proxy confiável (AA011); Api.RAG ganhou o mesmo `Transport:*` (ForwardedTransportTests). |
| Identidade | Adaptação | Produção exige OIDC: Auth0 (Authority com `/` final, audience por API, `scope` nos tokens M2M). |
| Token MCP | Adaptação | `KnowledgeMcp:BearerToken` fixo (M2M até 30 dias), rotação manual. |
| PostgreSQL | OK/Adaptação | Neon com `SSL Mode=Require`; migração cria `vector` (0001) e concede ao papel de aplicação, que precisa existir antes. Busca textual usa a configuração `portuguese` (padrão do PostgreSQL). |
| MongoDB | Não aplicável | Sem uso no código. |
| Health | Parcial | Agente: `/health/live`. RAG: sem health (decisão); usar 401 do `/mcp`. |
| Swagger | OK | Só em Development; não existe em Production. |
| GSSAPI (Npgsql) | Adaptação | Imagens instalam `libgssapi-krb5-2` (erro no log sem ela). |

## Arquitetura publicada

SOFIA/cliente → HTTPS → Vercel (proxy TLS) → container Api.Agente (`Transport:TrustedNetworks`)
→ HTTPS + JWT M2M → Render (proxy TLS) → container Api.RAG → Neon `rag` (pgvector).
Api.Agente → Neon `agente` (auditoria/consumo) e OpenAI (parecer). Api.RAG → OpenAI (embeddings).

## Arquivos

- `Dockerfile.vercel` (raiz, Api.Agente) e `src/Api.RAG/Dockerfile` (Render; contexto = raiz).
- `.dockerignore`: exclui bin/obj, testes, docs/specs (exceto o schema MCP), scripts, infra,
  `.env*`, `appsettings.Development.json` e `secrets.json`.
- `deploy/vercel-api-agente.env.example`, `deploy/render-api-rag.env.example`.
- `.github/workflows/ci.yml`.
- Código: `Transport:*` na Api.RAG (`RagOptions`, `RagOptionsValidator`, `Program`,
  `KnowledgeEndpointRegistration`) e testes `ForwardedTransportTests`.

## Descobrir a rede do proxy

Os proxies das plataformas não têm IP documentado aqui. No primeiro deploy, configurar
`Logging__LogLevel__Microsoft.AspNetCore.HttpOverrides=Debug` e uma rede provisória privada
(ex.: `10.0.0.0/8`); requisições de remetente fora da lista geram log "Unknown proxy" com o IP.
Fixar a menor rede observada e voltar o log ao padrão. Nunca usar `0.0.0.0/0` (o validador recusa).
Os containers só são alcançáveis pelo proxy da plataforma; por isso confiar na rede interna dele
é aceitável para a demo.

## Riscos e limites

- Vercel Hobby é para uso pessoal/não comercial; uso institucional da INFIS pode exigir plano pago.
- Container Runtime da Vercel em beta: comportamento e limites podem mudar.
- Render Free dorme: primeira chamada após ociosidade leva até ~1 min (`KnowledgeMcp:TimeoutSeconds` 60).
- Neon Free suspende compute ociosa (cold start de segundos) e limita armazenamento/compute.
- Auth0 Free limita tokens M2M por mês; token fixo expira e precisa ser trocado na Vercel.
- OpenAI cobra embeddings (~US$ 0,01 por ingestão do guia) e pareceres (~US$ 0,0013 cada).
- Cotas e preços devem ser conferidos nas páginas oficiais no dia do deploy.
