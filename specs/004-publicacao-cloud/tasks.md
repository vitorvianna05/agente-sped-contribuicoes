# Tasks — publicação cloud de demonstração

Data: 2026-11-12. Prefixo PC. Tarefas manuais (contas, painéis, segredos) são executadas pelo
usuário; nenhuma conta, recurso remoto ou deploy foi criado por esta entrega.

## Automatizáveis (concluídas)

- [X] PC001 Matriz de compatibilidade e decisões (plan.md, spec.md).
- [X] PC002 Api.RAG aceita TLS terminado em proxy confiável (`Transport:TrustedProxies/TrustedNetworks`), HTTP direto negado. Arquivos: `src/Api.RAG/Infrastructure/Configuration/RagOptions.cs`, `RagOptionsValidator.cs`, `src/Api.RAG/Program.cs`, `src/Api.RAG/Endpoints/KnowledgeEndpointRegistration.cs`, `tests/Api.RAG.Tests/Contracts/ForwardedTransportTests.cs`. Rastreio: FR-PC-003.
- [X] PC003 `Dockerfile.vercel`, `src/Api.RAG/Dockerfile` e `.dockerignore`. Rastreio: FR-PC-001.
- [X] PC004 Variáveis de ambiente de exemplo sem segredos (`deploy/`). Rastreio: FR-PC-002.
- [X] PC005 Workflow de CI (`.github/workflows/ci.yml`), passos executados localmente. Rastreio: FR-PC-005.
- [X] PC006 Build e fumaça local das imagens em Production (validation/local-smoke.md). Rastreio: SC-PC-001.

## Manuais (pendentes; roteiro em quickstart.md)

- [ ] PC007 Auth0: tenant, APIs "Api.RAG"/"Api.Agente" com permissões, aplicações M2M e expiração de token. Rastreio: FR-PC-003.
- [ ] PC008 Neon: projeto, bancos `rag` e `agente`, papéis de aplicação, migrações com papel dono (`--migrate`, `--migrate-agent`). Rastreio: FR-PC-004.
- [ ] PC009 Render: Web Service Docker da Api.RAG, variáveis, descoberta da rede do proxy, ingestão do Guia 1.35. Rastreio: FR-PC-002/003/006.
- [ ] PC010 Vercel: projeto com `Dockerfile.vercel`, variáveis, descoberta da rede do proxy, health público. Rastreio: FR-PC-002/003/006.
- [ ] PC011 Ponta a ponta público: análise sintética com evidências da RAG, consumo, logs sem segredos. Rastreio: SC-PC-003.
- [ ] PC012 Rollback e rotação: voltar deploy anterior em Vercel/Render; trocar o token M2M; registrar evidências. Rastreio: SC-PC-004.
- [ ] PC013 Primeira execução do CI no GitHub após o push. Rastreio: SC-PC-002.
