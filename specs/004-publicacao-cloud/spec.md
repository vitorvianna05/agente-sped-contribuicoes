# Feature 004 — Publicação cloud de demonstração

Data: 2026-11-12. Estado: artefatos prontos e validados localmente; nenhum recurso remoto criado.
Skill: `sofia-devops-free-cloud`. Responsável pelas decisões: Vitor Vianna.
Base: constitution, features 001–003 (contratos MCP/REST, borda HTTP da ADR 0002 da feature 003).

## Objetivo

Publicar as duas APIs existentes para demonstração, sem reescrever a arquitetura nem mudar
contratos: Api.Agente na Vercel (Container Runtime), Api.RAG no Render (Docker), PostgreSQL com
pgvector no Neon e identidade no Auth0. Somente dados sintéticos ou públicos (Guia Prático 1.35);
nenhum SPED real de cliente. "Gratuito" depende das cotas e termos vigentes de cada provedor;
custos de OpenAI não estão incluídos.

## Decisões do usuário (2026-11-12)

| Tema | Decisão |
| --- | --- |
| Hospedagem | Api.Agente na Vercel Hobby (container); Api.RAG no Render Free (Docker), pois tem workers residentes. |
| Banco | Neon Free: bancos `rag` (pgvector) e `agente`. MongoDB Atlas não é usado: o projeto não tem memória/histórico em Mongo. |
| Identidade | Auth0 Free: APIs "Api.RAG" e "Api.Agente", aplicações M2M para Agente→RAG, administração da RAG e SOFIA→Agente. |
| Dados | Demo com dados sintéticos/públicos; políticas marcadas como não aprovadas para produção. |
| Health na RAG | Não adicionar endpoint; validar pela resposta 401 do `/mcp`. |
| TLS | TLS termina no proxy da plataforma; as duas APIs aceitam `X-Forwarded-Proto` só de proxies/redes configurados. |
| Token Agente→RAG | Token M2M fixo em variável de ambiente, com rotação manual antes de expirar. |

## Requisitos

| ID | Requisito verificável |
| --- | --- |
| FR-PC-001 | Imagens multi-stage .NET 10 a partir dos `.csproj` reais, restore `--locked-mode`, usuário não-root, escuta em `0.0.0.0:$PORT`, sem segredos nem `appsettings.Development.json` na imagem. |
| FR-PC-002 | Configuração de produção somente por variáveis de ambiente com os nomes lidos por `IOptions` (exemplos em `deploy/*.env.example`, sem valores secretos). |
| FR-PC-003 | HTTP direto é recusado; HTTPS terminado no proxy só é aceito de `Transport:TrustedProxies/TrustedNetworks`. JWT continua obrigatório em todas as rotas protegidas. |
| FR-PC-004 | Migrações executadas fora do boot, com papel de migração distinto do papel de aplicação (`--migrate`, `--migrate-agent`), sobre Neon com `SSL Mode=Require`. |
| FR-PC-005 | CI no GitHub: restore travado, build, format, testes com Docker e build das duas imagens; sem publicação nem segredos. |
| FR-PC-006 | Roteiro reproduzível (Auth0, Neon, Render, Vercel), teste ponta a ponta público, rollback e limites/riscos de cada provedor. |

## Aceite

- SC-PC-001: build e fumaça local das imagens em Production: health 200 via proxy, 403/400 em
  HTTP direto, 401 sem token, RAG conectada ao PostgreSQL com workers ativos (validation/local-smoke.md).
- SC-PC-002: CI verde no GitHub (passos idênticos aos executados localmente).
- SC-PC-003 (manual, pendente): na URL pública, Agente → RAG por MCP com token Auth0 devolve
  evidências do guia ingerido no Neon, consumo registrado e nenhum segredo em logs/respostas.
- SC-PC-004 (manual, pendente): rollback de deploy testado em Vercel e Render.

## Fora do escopo

Endpoint A2A e Agent Card (feature 003, AA005/AA007); dados reais; SLA; MongoDB/memória;
cliente de credenciais do Auth0 na Api.Agente (token fixo por decisão).
