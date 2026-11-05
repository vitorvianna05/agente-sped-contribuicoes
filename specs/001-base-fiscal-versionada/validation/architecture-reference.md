# Revisão documental por referência — 2026-11-05

Escopo autorizado: atualizar documentação/estrutura planejada do projeto SPED, usando
Api.Conhecimento e o cliente Api.Atendimento da solução CorporativoAI como referência.
Esclarecimento: IA generativa interpreta resultados dos validadores e produz parecer com evidências.

## Alterações documentais

- docs/architecture.md e docs/requirements.md passam a apresentar o fluxo completo.
- docs/architecture/conhecimento-fiscal.md mantém invariantes e aponta para o desenho integrado.
- Feature 001 atualiza especificação (FR-022), plano, pesquisa, contratos REST/MCP, schema e
  tasks abertas para Controllers/Ingestao/Rag/Tools; nenhuma task executada foi reaberta ou
  tarefa pendente marcada como concluída.
- Feature 002 inclui spec/plan/tasks para validação determinística SPED, cliente MCP e parecer
  OpenAI, com todos os itens AG001–AG014 pendentes e dependências/gates explícitos.
- ADR-0004 registra decisões, diferenças contratuais frente à referência e compatibilidade.

## Verificação desta revisão

Somente Markdown e JSON de contratos foram alterados/criados neste repositório. Código,
projetos, lockfiles, constitution, configurações locais e evidências TRX anteriores foram
preservados. A solução externa foi somente lida; não foi compilada, executada ou modificada.
Não foram lidos arquivos de secrets/appsettings externos nem copiadas credenciais.

Verificar sintaxe JSON, consistência dos nomes/obrigatórios das tools, contagem de tasks e
ausência de mudanças em código. Não executar build/testes para alegar implementação de um
desenho documental. Os 41 testes anteriores continuam evidência da fundação anterior, sem
provar as novas tools, rota de ingestão, cliente Api.Agente ou geração de parecer.

Não foram aprovados modelos, dimensões, parâmetros, fontes/layouts ou prazos de produção.
Nenhum deploy, commit ou push faz parte desta revisão.

Resultado da verificação: JSON e referencias locais dos schemas MCP válidos; nomes BuscarConhecimento/ResolverReferencia e obrigatórios consulta/tipoDocumento/versaoLayout conferidos. Feature 001 preserva 84 tasks, 18 concluídas; feature 002 tem 14 tasks pendentes. git diff --check sem erros. Diff de arquivos rastreados limitado a docs/ e specs/, sem código ou projetos alterados.
