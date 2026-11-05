# Requisitos globais — validação SPED Contribuições com IA

Atualização autorizada em 2026-11-05. Este documento complementa, sem substituir,
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

Similaridade é ranking técnico. A IA não substitui os validadores obrigatórios e não
converte texto de documento em instrução para alterar permissões ou regras.
Não há implementação de login por ChatGPT nem automação de sua interface: a integração
usa modelos OpenAI por API, com credencial e configuração externas.

Arquitetura: [architecture.md](architecture.md). Fontes/layouts oficiais específicos,
regras abrangidas, responsáveis, critérios de avaliação e prazos reais continuam pendentes
de evidência e aprovação; este texto não fabrica esses insumos.
