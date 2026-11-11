# Tasks — validação SPED e parecer

Desenho: 2026-11-05; sincronização: 2026-11-10. AG002/003/004/007/008/009/010/015 implementadas: 8 tasks concluídas e 7 com pendências, detalhadas ao final. Prefixo AG evita colisão com T001–T084 da feature 001.
Dependências abaixo são obrigatórias para aceite integral. Núcleo parametrizado AG011/012 e parte sintética de AG013 implementados, sem encerrar predecessores fiscais/política. Evidências atuais em validation/interoperability.md; histórico em validation/initial-mcp.md.

- [ ] AG001 Registrar fontes/layouts/regras determinísticas e política de dados do SPED com responsáveis e evidências; não fabricar regras ou prazo. Arquivos: `validation/supported-layouts.md`, `validation/data-policy.md`. Depende: nenhuma. Rastreio: FR-AG-002/009.
- [X] AG002 Criar Api.Agente .NET 10 e testes com organização Controllers/Analise/Validacao/Mcp/Llm/Contracts/Domain/Infrastructure, configuração externa e providers sintéticos isolados. Arquivos: `src/Api.Agente/Api.Agente.csproj`, `tests/Api.Agente.Tests/Api.Agente.Tests.csproj`. Depende: nenhuma. Rastreio: FR-AG-001/009; testes não ativam dados reais de AG001.
- [X] AG003 Escrever testes das entradas, estados técnicos e contratos separados do envelope e do JSON do LLM. Arquivos: `tests/Api.Agente.Tests/Contracts/AnalysisContractTests.cs`. Depende: AG002. Rastreio: FR-AG-001/008; SC-AG-003.
- [X] AG004 Implementar autenticação, autorização, limites/cancelamento e execução/correlação do sistema, sem confiar em ator no corpo. Arquivos: `src/Api.Agente/Infrastructure/Security/AnalysisAuthorization.cs`, `src/Api.Agente/Program.cs`. Depende: AG003. Rastreio: FR-AG-001/009.
- [ ] AG005 Escrever e executar testes de identificação/parsing e regras determinísticas com fixtures sintéticas identificadas e casos oficiais autorizados. Arquivos: `tests/Api.Agente.Tests/Unit/SpedValidationTests.cs`. Depende: AG003; casos oficiais exigem AG001. Rastreio: FR-AG-002; SC-AG-001.
- [ ] AG006 Implementar identificação, parser e validadores somente para layouts/regras aprovados, com resultados e verificações não realizadas explícitos. Arquivos: `src/Api.Agente/Validacao/SpedValidator.cs`, `src/Api.Agente/Domain/ValidationResult.cs`. Depende: AG004, AG005, AG001. Rastreio: FR-AG-002.
- [X] AG007 Escrever testes do cliente MCP, falha técnica/IsError, schema/referência inválida e contexto de layout obrigatório. Arquivos: `tests/Api.Agente.Tests/Contracts/KnowledgeClientTests.cs`. Depende: AG003, feature 001 T041. Rastreio: FR-AG-004; SC-AG-002/003.
- [X] AG008 Implementar IConhecimentoClient e ConhecimentoMcpClient com HttpClientTransport/CreateAsync/ListToolsAsync/CallToolAsync, DTOs locais e configuração autenticada; sem fallback REST ou SQL. Arquivos: `src/Api.Agente/Mcp/IConhecimentoClient.cs`, `src/Api.Agente/Mcp/ConhecimentoMcpClient.cs`, `src/Api.Agente/Mcp/ConhecimentoMcpOptions.cs`. Depende: AG004, AG007, feature 001 T043. Rastreio: FR-AG-003/004.
- [X] AG009 Escrever testes de preservação dos resultados, JSON inválido, citações inventadas, injeção em trechos e ausência de evidência. Arquivos: `tests/Api.Agente.Tests/Unit/ParecerValidationTests.cs`, `tests/Api.Agente.Tests/Unit/AnalysisPipelineTests.cs`. Depende: AG003, AG007. Rastreio: FR-AG-005/006; SC-AG-001/002/003. Casos sintéticos; comportamento adversarial do modelo real e qualidade da prosa pertencem a AG014.
- [X] AG010 Implementar IParecerLlm/OpenAiParecerLlm com gpt-4.1-mini registrado na decisão mais recente (ADR 0010 da Feature 001), configurável e sujeito a AG014, e prompt versionado com resultados determinísticos + evidências; validar saída estrita e citações antes de publicar. Arquivos: `src/Api.Agente/Analise/AnalysisPorts.cs`, `src/Api.Agente/Llm/OpenAiParecerLlm.cs`, `src/Api.Agente/Contracts/ParecerValidator.cs`, `src/Api.Agente/Llm/Prompts/parecer-v1.txt`. Depende: AG009, AG008. Rastreio: FR-AG-005/006.
- [ ] AG011 Implementar consumo/auditoria por tentativa e políticas de persistência da Api.Agente, com moedas estimada/informada separadas; correlacionar MCP sem duplicar consumo de embeddings da Api.RAG. Arquivos: `src/Api.Agente/Infrastructure/Consumption/MeasuredParecerLlm.cs`, `tests/Api.Agente.Tests/Integration/ConsumptionTests.cs`. Depende: AG010, AG001. Rastreio: FR-AG-007/009; SC-AG-004.
- [ ] AG012 Coordenar AnaliseSpedService reutilizável por REST técnico e pelo futuro adaptador A2A SOFIA (Feature 003), com POST /api/v1/analises como adaptador, e validadores → MCP → LLM → validação da saída, retornando resultados/limitações/revisão humana; bloquear etapas dependentes em falha impeditiva. Arquivos: `src/Api.Agente/Analise/AnaliseSpedService.cs`, `src/Api.Agente/Controllers/AnalisesController.cs`. Depende: AG006, AG008, AG010, AG011. Rastreio: FR-AG-001–009.
- [X] AG013 Executar ponta a ponta entre APIs separadas com layout conhecido, negativo, falha MCP, falha OpenAI e referência inválida; validar tokens, isolamento e ausência de conformidade indevida. Arquivos: `tests/Api.Agente.Tests/Integration/AnalysisJourneyTests.cs`, `validation/interoperability.md`. Depende: AG012, feature 001 T049. Rastreio: SC-AG-001–004.
- [ ] AG014 Aprovar corpus/rubrica/modelo gerador, executar avaliação oficial, medir custo/latência completa e revisar contratos/DoD com evidências; não concluir com insumos ausentes ou critério falho. Arquivos: `evaluation/manifest.json`, `evaluation/parecer-report.md`, `validation/verification-report.md`. Depende: AG013, AG001, feature 001 T077 e gates reais de ambas as APIs. Rastreio: SC-AG-005.

- [X] AG015 Adicionar OpenAPI/Swagger UI de Development para testar leitura/análise/consumo/diagnóstico, com upload bruto binário, JWT por operação e respeito ao modo local/transporte; verificar ausência em Production. Arquivos: `src/Api.Agente/Infrastructure/OpenApi/AgentOpenApi.cs`, `tests/Api.Agente.Tests/Integration/SwaggerTests.cs`. Depende: AG004 e adaptadores REST de AG012 (implementados parametrizados). Rastreio: FR-AG-001/008/009. Solicitação explícita do usuário em 2026-11-10; não habilita política ou suporte fiscal pendentes. Evidência: `validation/swagger.md`.

Os caminhos validation/evaluation são relativos a esta feature; src/tests são relativos à raiz.
A integração A2A está planejada na Feature 003, ainda não implementada. Memória e configuração
de produção não são presumidas por este plano.

Dependência atual: o servidor MCP da Feature 001 está implementado (T041/T043); a última
suíte documentada possui 97 testes aprovados (2026-11-10; publicação automática pela ADR 0009, contrato MCP inalterado). A matriz T049 e os gates de operação real
continuam pendentes. AG002/AG003/AG004/AG007/AG008 foram executadas com fixtures e servidor MCP separado.
A nova API aplica sua própria exceção Development loopback e mantém JWT no modo normal;
isso não autoriza acesso externo nem fecha P04. Modelo escolhido não fecha a avaliação AG014.

## Etapa 3 — fundação e cliente MCP (2026-11-10)

- AG002: Api.Agente + Api.Agente.Tests net10.0, configuração externa, lockfiles e inclusão
  dos projetos na solução, preservando os itens existentes. Nenhum ProjectReference à RAG.
- AG003: metadados estritos, estados técnicos e contratos separados do envelope e ParecerDraft;
  não implementa AG009/AG010 nem promete formato fiscal aprovado das validações.
- AG004: JWT/subject/permissões, diagnóstico com atribuição administrativa, limites HTTP,
  timeout/cancelamento, execução/correlação do sistema e modo Development restrito a loopback.
  Persistência própria de consumo/auditoria permanece AG011.
- AG007/AG008: HttpClientTransport/Create/List/Call, schemas congelados/validação local,
  bearer externo de serviço, limites de resposta, cancelamento e falhas explícitas; sem SQL,
  fallback ou consulta funcional REST. Servidor sintético independente em outro processo.

GET /admin/v1/knowledge/readiness é diagnóstico restrito, não análise SPED: retorna
analysisAvailable=false. Integração com a versão final concorrente da RAG ainda deve ser
verificada após sua estabilização; AG013 e os gates reais continuam pendentes.
Não foram modificados os arquivos da Api.RAG nem as tasks da Feature 001 nesta etapa.

Guia: [quickstart.md](quickstart.md). Contratos: [foundation.md](contracts/foundation.md).
Decisão: [ADR 0001](adrs/0001-fundacao-cliente-mcp.md). Evidências: [initial-mcp.md](validation/initial-mcp.md).

## Incremento autorizado — parser de leitura (2026-11-10)

O usuário solicitou adicionar parser para leitura e forneceu o Guia Prático EFD Contribuições
1.35 (18/06/2021). Fonte/páginas/hash registrados em validation/supported-layouts.md.

- [X] Leitor estrutural Latin-1 streaming com linhas/registros/campos preservados, limites,
  cancelamento, SHA-256 e ocorrências estruturais baseadas no manual fornecido.
- [X] POST /api/v1/sped/leitura com analysis.execute, corpo bruto, sem persistência/IA/MCP,
  no-store e estados separados de leitura e validação fiscal.
- [X] Testes sintéticos unitários/HTTP: vazios/acentos/zeros/CRLF/envelope/contagem, limites,
  codificações incompatíveis, permissão e upload sem Content-Length.

Este é um subconjunto de AG005/AG006; suas caixas continuam abertas porque catálogo de
leiautes, semântica/hierarquia por registro, regras fiscais e casos oficiais não foram
implementados/aprovados. AG001 recebeu uma fonte, mas política de dados e aprovações
restantes continuam pendentes. Nenhuma task de parecer/análise integral foi encerrada.

Arquivos: SpedDocument/SpedFileStructure em Domain, ISpedParser/SpedParser em Validacao,
Controllers/SpedReadingController.cs, SpedParserTests e SpedReadingTests. Decisão:
[ADR 0002](adrs/0002-parser-sped-leitura.md). Contrato: [parser-sped](contracts/parser-sped.md).
Evidência: [parser-sped](validation/parser-sped.md).

## Integração com a orquestradora

SOFIA/INFIS é a consumidora externa confirmada; a Feature 003 possui AA001–AA010 para o
contrato compartilhado, texto SPED, Task/Artifact, segurança e testes de interoperabilidade.
AG012 possui núcleo reutilizável parametrizado; AG013 testa análise/MCP e não substitui
o aceite A2A AA008/AA009. A alteração da descrição de AG012 não muda seus predecessores
nem conclui tasks. Nenhum componente da RAG é alterado por esse desenho.

## Incremento — fluxo MCP/parecer e persistência (2026-11-10)

O usuário confirmou escopo temporal 2020–2026. Isso não aprova correspondência COD_VER/ano.
tipoDocumento da RAG continua indefinido; prazos/referência da política e conexão própria
serão fornecidos/configurados pelo usuário. Não se cadastram perfis ou políticas reais por suposição.

| Task | Entrega técnica e condição restante |
| --- | --- |
| AG001 | Fonte do manual e resposta do usuário registradas. Pendente catálogo/aplicabilidade/aprovação fiscal e política real. |
| AG005/006 | Parser/envelope/contagem/datas, período 2020–2026, mapeamento externo inequívoco, testes negativos e unsupported implementados. Pendente validação integral de campos/blocos/regras e corpus oficial aprovado. |
| AG009/010 | JSON/schema/prompt v1, SDK OpenAI real com HTTP simulado nos testes, citações resolvidas, pendências preservadas, recusa/incompleto/retries finitos. Avaliação oficial não presumida. |
| AG011 | Intenção antes da tentativa, uso conhecido inclusive falhas, PostgreSQL próprio/migração/papéis segregados, consulta isolada e expurgo por lotes implementados/testados. Pendente política/conexão reais e operação aprovada. Custo estimado por `Llm:Pricing` com tokens em cache (ADR 0004); sem tabela ou uso, NULL; faturamento do provedor NULL. |
| AG012 | AnaliseSpedService + POST /api/v1/analises + GET consumo implementados para cobertura estrutural parcial. Depende de AG001/006/011 para aceite integral; não retorna conformidade nem completed. |
| AG013 | Concluída em 2026-11-11: CrossApiJourneyTests contra a Api.RAG real em outro processo (JWT, PgVector, contrato 1.0) com layout conhecido, sem documentos, falha de embeddings, token recusado e falhas do gerador; jornada sintética anterior cobre referência inválida. Provedores (embeddings/LLM) sintéticos na suíte; não é aprovação produtiva nem avaliação (AG014). |
| AG014 | Pendente corpus/rubrica/revisores, chamada real autorizada/avaliada, custo/latência e aceite fiscal/produção. |

125 testes aprovados, zero ignorados; [evidência e rastreabilidade](validation/interoperability.md).
[Contrato REST](contracts/analysis-rest.md), [ADR 0003](adrs/0003-fluxo-parecer-auditoria.md),
[política pendente](validation/data-policy.md). Nenhum arquivo da Api.RAG/Feature 001 foi
editado por este incremento. AA001–AA010 da Feature 003 não foram implementadas.

## Incremento — Swagger (2026-11-10)

AG015 concluída: /swagger e /openapi/v1.json em Development, seletor de arquivo bruto nos
dois POSTs, esquema JWT baseado na autenticação ativa, health anônimo e documentação das
permissões/estados/pendências. Perfil de execução existente já abre /swagger. Sem mudanças
na Api.RAG ou no contrato funcional de upload. Suíte atual: 129 aprovados, zero falhas/ignorados;
quatro testes novos em SwaggerTests. [Evidência](validation/swagger.md).

## Incremento — custo estimado do parecer (2026-11-11)

Parte de AG011. ADR 0004: tabela `Llm:Pricing` (input, cache, output, moeda, referência e versão),
tokens em cache lidos do provedor, migração `0002_llm_cached_tokens` e custo estimado gravado por
tentativa. Sem tabela, custo NULL. Execução real local: US$ 0,001306 por análise com 98% do input em
cache. Suíte atual: 138 aprovados, zero falhas/ignorados. AG011 segue aberta por política/conexão reais.

## Incremento — ponta a ponta com a Api.RAG real (2026-11-11)

AG013 concluída. Api.RAG.Tests ganhou o modo `--rag-server` (Api.RAG real com PostgreSQL próprio,
PDF sintético publicado e token de serviço); Api.Agente.Tests o inicia uma vez por classe
(`RealRagServer`) e executa 6 jornadas em CrossApiJourneyTests. Referência de compilação sem
assembly (`ReferenceOutputAssembly=false`): nenhum tipo da RAG entra na Api.Agente. Suíte atual:
144 aprovados, zero falhas/ignorados. [Evidência](validation/interoperability.md).
