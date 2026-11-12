# Evidência — build e fumaça local das imagens (PC003/PC006)

Data: 2026-11-12. Docker Desktop 29.8.2. Sem rede externa, sem chaves reais, sem deploy.

## Build

- `docker build -f Dockerfile.vercel -t agente-sped/api-agente:local .` → sucesso (~358 MB).
- `docker build -f src/Api.RAG/Dockerfile -t agente-sped/api-rag:local .` → sucesso (~366 MB).
- Primeira tentativa da RAG falhou: o schema MCP embutido fica em `specs/001-.../contracts`;
  corrigido liberando só esse arquivo no `.dockerignore` e copiando-o no Dockerfile.
- Conteúdo de `/app`: apenas `appsettings*.Example.json` (sem `appsettings.Development.json`,
  `.env` ou `secrets.json`); processo como UID 1654 (não-root).

## Fumaça em Production

Variáveis montadas a partir de `deploy/*.env.example` com valores sintéticos (Authority fictícia,
token fictício, `Transport:TrustedNetworks` 172.16.0.0/12 e 192.168.0.0/16). A RAG usou o
PostgreSQL local pela rede do compose, com o papel `rag_application`; arquivos de variáveis
temporários apagados após o `docker run`.

| Chamada | Resultado |
| --- | --- |
| Agente `GET /health/live` com `X-Forwarded-Proto: https` | 200 |
| Agente `GET /health/live` em HTTP direto | 403 `TRANSPORT_DENIED` |
| Agente `POST /api/v1/analises` sem token (via proxy) | 401 |
| RAG `POST /mcp` sem token (via proxy) | 401 |
| RAG `POST /mcp` em HTTP direto | 400 `INVALID_ARGUMENT` |

Logs: somente falhas de autorização esperadas. A primeira execução registrou
`libgssapi_krb5.so.2: cannot open shared object file` (Npgsql); as imagens passaram a instalar
`libgssapi-krb5-2` e o erro não se repetiu.

## Passos do CI executados localmente

`dotnet restore AgenteSpedContrib.slnx --locked-mode`, `dotnet build --no-restore` (0 avisos/erros),
`dotnet format --verify-no-changes`, Api.RAG.Tests 115/115 (12 novos em ForwardedTransportTests),
Api.Agente.Tests 163/163. A execução no GitHub fica para PC013.
