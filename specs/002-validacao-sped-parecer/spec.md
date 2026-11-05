# Especificação — validação SPED Contribuições e parecer com IA

Data: 2026-11-05. Estado: desenho documental inicial; nenhuma task implementada.
Base normativa: constitution 1.1.0 e docs/requirements.md.

## Solicitação e esclarecimento

O usuário solicitou arquitetura semelhante à Api.Conhecimento da solução CorporativoAI:
embeddings OpenAI, PostgreSQL/PgVector e comunicação MCP entre APIs. Confirmou que a IA
generativa deve **interpretar os resultados das validações e elaborar o parecer com evidências**.
O objetivo permanece validar arquivos SPED Contribuições.

## Escopo

Api.Agente recebe o arquivo, identifica o layout, executa validadores determinísticos,
consulta o servidor Api.RAG por MCP e utiliza um modelo gerador OpenAI para explicar os
resultados e elaborar o parecer. Esta feature consome a base da feature 001; não duplica
ingestão, armazenamento vetorial, aprovação documental ou credenciais do banco RAG.
Memória conversacional e integração A2A serão planejadas em incrementos próprios.

## Histórias e aceite

### US-AG1 — executar validações determinísticas (P1)

Como solicitante autenticado, envio um SPED Contribuições e recebo a identificação,
os resultados das verificações suportadas e uma relação explícita das não realizadas.

- Arquivo inválido, acima de limite ou layout não suportado impede etapas dependentes.
- Tipo e versão são identificados pelo sistema; não são adivinhados pelo modelo.
- Cada regra executada identifica fonte oficial/versão e seu resultado determinístico.
- Inconsistências não tornam a execução tecnicamente falha; são parte do resultado da análise.

### US-AG2 — recuperar evidências via MCP (P1)

Como serviço consumidor, descubro a tool BuscarConhecimento e a invoco com consulta,
tipoDocumento e versaoLayout, recebendo somente evidências aplicáveis e referências resolvíveis.

- ConhecimentoMcpClient usa HttpClientTransport/CreateAsync/ListToolsAsync/CallToolAsync.
- Token e correlação pertencem ao transporte; não vêm do texto ou dos argumentos livres.
- IsError, JSON ou referência inválida geram falha controlada, sem fallback REST ou acesso SQL.
- Falta de evidência não equivale a validação fiscal aprovada.

### US-AG3 — interpretar e elaborar parecer (P1)

Como solicitante, recebo explicação dos resultados e inconsistências, fundamentada nos
trechos recuperados. O modelo recebe resultados determinísticos e contexto mínimo necessário.

- O texto do parecer cita somente referenceIds efetivamente recuperados e validados.
- A saída do LLM é JSON estrito e separada do envelope HTTP.
- O LLM não inventa regras, não altera resultados dos validadores e não transforma pendência
  em conformidade. Solicitação maliciosa em arquivo/trecho permanece dado não confiável.
- JSON inválido, referência inventada ou indisponibilidade do modelo impede publicar o parecer
  como concluído; registrar limitações e resultado técnico adequado.

## Requisitos funcionais

| ID | Requisito |
| --- | --- |
| FR-AG-001 | Receber arquivo por REST autenticado, com limites externos e execução/correlação do sistema. |
| FR-AG-002 | Identificar e validar apenas layouts/regras oficialmente suportados; expor verificações não realizadas. |
| FR-AG-003 | Construir consultas a partir dos resultados e contexto identificados, sem enviar o arquivo fiscal inteiro por padrão ao provedor. |
| FR-AG-004 | Consumir MCP exclusivamente para recuperar/resolver conhecimento da Api.RAG, com contratos de feature 001. |
| FR-AG-005 | Usar IA generativa OpenAI para interpretar os resultados e redigir parecer com evidências, sem substituir as regras determinísticas. |
| FR-AG-006 | Validar esquema e semântica da saída, preservando resultados dos validadores e confrontando cada citação com o contexto. |
| FR-AG-007 | Registrar por tentativa tokens, custos disponíveis, modelo, duração, correlação e falhas; desconhecidos permanecem nulos. |
| FR-AG-008 | Retornar envelope com executionId, correlationId, status técnico, validações, inconsistências, parecer, evidências, limitações, verificações não realizadas e revisão humana. |
| FR-AG-009 | Aplicar isolamento de acesso aos arquivos/resultados, retenção aprovada e proteção dos dados enviados ao modelo. |

## Estados e contrato de saída planejado

`completed`, `partial`, `failed` e `unsupported` descrevem o processamento técnico.
`completed` não significa conformidade tributária. Falhas impeditivas interrompem etapas
dependentes; `partial` declara todas as verificações obrigatórias não realizadas e não
autoriza conclusão de conformidade. Severidades fiscais e critérios substantivos dependem
de fontes aprovadas, não são criados por este documento.

JSON do LLM: explicação, inconsistências interpretadas, citações por referenceId,
limitações e indicação fundamentada de revisão humana. IDs, status técnico consolidado,
consumo/custos/tempos e verificações executadas são atribuídos pelo sistema. O modelo não
pode reclassificar um resultado determinístico para ocultar erro ou criar uma validação nova.

## Critérios de sucesso

- SC-AG-001: cenários suportados mantêm resultados determinísticos idênticos com provider fake ou real.
- SC-AG-002: nenhum cenário de aceite usa trecho de versão incompatível ou referência não recuperada.
- SC-AG-003: saídas inválidas, falhas externas e verificações pendentes nunca são apresentadas como conformidade.
- SC-AG-004: cada chamada/retry de embeddings ou geração tem proprietário e registro auditável; sem contabilização dupla entre serviços.
- SC-AG-005: avaliação do parecer usa conjunto/rubrica fiscal aprovados antes da aceitação, com resultados e revisores identificáveis.

O alvo de 5s da feature 001 mede recuperação MCP e exclui parecer. SLA/volume da análise
completa e rubrica de qualidade do texto permanecem pendentes de definição e aprovação.

## Entradas pendentes para implementação/aceite

Fontes, layouts, regras e exemplos oficiais suportados; política de dados/retensão do SPED;
emissor/audience/permissões da Api.Agente; modelo gerador e parâmetros avaliados; rubrica de
parecer e responsáveis fiscais; limites/latência da análise completa. Código parametrizado
e testes sintéticos podem avançar; não ativar dados reais ou declarar qualidade oficial sem esses insumos.
