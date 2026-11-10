# Requisitos globais — validação SPED Contribuições com IA

Revisão de escopo autorizada em 2026-11-05; estado sincronizado em 2026-11-10. Este documento complementa, sem substituir,
a constitution 1.1.0 e as especificações rastreáveis de cada feature.

| ID | Requisito | Entrega |
| --- | --- | --- |
| RG-001 | Receber arquivos SPED Contribuições e identificar formato/versão do layout sem presumir suporte. | Feature 002 |
| RG-002 | Validar estrutura e regras determinísticas aprovadas a partir de documentação oficial versionada. | Feature 002 |
| RG-003 | Ingerir manuais/guias oficiais PDF, segmentar e gerar embeddings reais via API OpenAI. | Feature 001, T019–T034 |
| RG-004 | Armazenar vetores/textos/proveniência em PostgreSQL/PgVector e recuperar trechos por similaridade cosseno e elegibilidade fiscal. | Feature 001, T035–T043 |
| RG-005 | Usar servidor MCP na Api.RAG e ConhecimentoMcpClient na Api.Agente, no padrão da Api.Conhecimento/Api.Atendimento de referência. | Features 001 e 002 |
| RG-006 | Usar modelo gerador OpenAI para interpretar resultados determinísticos e elaborar parecer com as evidências recuperadas. | Feature 002; esclarecimento confirmado pelo usuário |
| RG-007 | Validar JSON e citações do parecer; referências devem existir no contexto recuperado e resolver o trecho exato. | Feature 002 |
| RG-008 | Distinguir processamento concluído, parcial, falha técnica e layout não suportado; não apresentar pendências como conformidade. | Features 001 e 002 |
| RG-009 | Aplicar DDD, Clean Code, autorização, identidade do transporte, retenção e custos por tentativa; preservar campos desconhecidos nulos. | Transversal |
| RG-010 | Separar modelo de embeddings do modelo gerador; ambos configuráveis, versionados e avaliados sem escolher defaults fiscais por convenção. | Transversal |
| RG-011 | SOFIA/INFIS recebe a solicitação inicial e seleciona os agentes; Api.Agente recebe chamadas A2A como especialista SPED e devolve análise rastreável para agregação pela SOFIA, sem assumir roteamento multiagente. | Feature 003; esclarecimento confirmado pelo usuário |

Similaridade é ranking técnico. A IA não substitui os validadores obrigatórios e não
converte texto de documento em instrução para alterar permissões ou regras.
Não há implementação de login por ChatGPT nem automação de sua interface: a integração
usa modelos OpenAI por API, com credencial e configuração externas.

Arquitetura: [architecture.md](architecture.md). Fontes/layouts oficiais específicos,
regras abrangidas, responsáveis, critérios de avaliação e prazos reais continuam pendentes
de evidência e aprovação; este texto não fabrica esses insumos.

## Decisões e estado sincronizados

Api.RAG possui implementação técnica simplificada (ingestão com publicação automática e busca
via MCP, [ADR 0009](../specs/001-base-fiscal-versionada/adrs/0009-simplificacao-ingestao-busca.md))
com 97 testes sintéticos aprovados; a Api.Agente
já tem fundação MCP, leitura estrutural e fluxo parametrizado de análise/parecer com auditoria
própria e Swagger em Development (129 testes aprovados em 2026-11-10). Validação fiscal integral, aceite real do parecer
e servidor A2A permanecem pendentes. Isso não encerra critérios fiscais, de capacidade e de operação real.
O usuário escolheu text-embedding-3-small (1536 dimensões no exemplo local) para embeddings
e gpt-4.1-mini (`OpenAI:ChatModel`, [ADR 0010](../specs/001-base-fiscal-versionada/adrs/0010-ambiente-local-openai-swagger.md);
antes gpt-4o-mini) para interpretar validações e elaborar parecer na Api.Agente.
Na Api.Agente, a configuração própria é Llm:Model/Llm:ApiKey, sem herdar segredos da RAG.
São configurações externas/versionadas; escolha de modelo não substitui avaliação de RG-010.

Exceção local autorizada: dispensa de JWT em Development loopback, com identidade de serviço
local e HTTPS/hosts mantidos. Api.Agente implementa seu próprio controle e não herda o da RAG;
as flags são bloqueadas em outros ambientes.
RG-009 continua obrigatório na operação normal; a exceção não aprova integração externa,
retenção ou P04. [ADR 0008](../specs/001-base-fiscal-versionada/adrs/0008-configuracao-local-modelos.md).
Estado e lacunas: [sincronização SDD](../specs/001-base-fiscal-versionada/validation/sdd-synchronization.md).

## SOFIA como consumidora da Api.Agente

Confirmado pelo usuário: a orquestradora é SOFIA/INFIS. Fluxo alvo: usuário → SOFIA →
Api.Agente → SOFIA → usuário. Perfil A2A v0.3.x JSON-RPC; contrato de negócio SOFIA 1.0,
com IDs preservados e usuário de negócio distinto da identidade autenticada do transporte.
O MCP interno da Api.Agente para a RAG é preservado integralmente; a orquestradora não recebe
credencial nem acesso ao banco dessa base.

A fundação MCP, leitura SPED e análise estrutural/rascunho de parecer da Api.Agente estão
implementadas; servidor A2A, validação fiscal integral e avaliação oficial continuam pendentes.
Sem política própria/conexão e contexto oficial mapeado não se habilita análise real. REST não é entrada A2A.
Especificação/contrato/tarefas: [Feature 003](../specs/003-integracao-sofia-a2a/spec.md).
A confirmação orienta documentação; não implementa ou ativa uma integração externa.

Períodos desejados: 2020–2026, sem presumir COD_VER. Tipo documental da RAG e política de
dados ainda serão definidos/fornecidos pelo usuário. [Contrato implementado](../specs/002-validacao-sped-parecer/contracts/analysis-rest.md)
e [evidência/pendências](../specs/002-validacao-sped-parecer/validation/interoperability.md).
