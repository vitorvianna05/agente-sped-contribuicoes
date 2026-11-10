# Especificação — validação SPED Contribuições e parecer com IA

Desenho inicial: 2026-11-05. Sincronização: 2026-11-10. Estado: fundação/MCP/parecer/Swagger implementados e núcleo de análise parametrizado; 8 tasks concluídas e 7 com pendências de implementação/aceite.
Base normativa: constitution 1.1.0 e docs/requirements.md.

## Solicitação e esclarecimento

O usuário solicitou arquitetura semelhante à Api.Conhecimento da solução CorporativoAI:
embeddings OpenAI, PostgreSQL/PgVector e comunicação MCP entre APIs. Confirmou que a IA
generativa deve **interpretar os resultados das validações e elaborar o parecer com evidências**.
O objetivo permanece validar arquivos SPED Contribuições.

## Decisões confirmadas e dependência atual

O usuário escolheu gpt-4.1-mini para interpretar resultados e elaborar parecer (2026-11-10;
substitui gpt-4o-mini, [ADR 0010 da feature 001](../001-base-fiscal-versionada/adrs/0010-ambiente-local-openai-swagger.md)).
O padrão da Api.Agente (`Llm:Model` e `LlmOptions`) está alinhado à decisão mais recente.
O modelo é configurável e o prompt versionado; a escolha não encerra a avaliação
oficial AG014. Os embeddings são responsabilidade da Api.RAG, com text-embedding-3-small/1536.
A RAG tem servidor MCP e jornadas sintéticas comprovadas (97 testes da Feature 001); desde a
[ADR 0009](../001-base-fiscal-versionada/adrs/0009-simplificacao-ingestao-busca.md) os documentos
ingeridos ficam pesquisáveis automaticamente, sem aprovação. Contrato das tools inalterado.
Matriz completa de falhas T049 e gates P02–P04 permanecem pendentes.

A dispensa de JWT solicitada está implementada somente na Api.RAG em Development loopback.
Api.Agente agora implementa controles próprios de AG004 e dispensa JWT somente em
Development loopback, preservando HTTPS/hosts. Isso não presume autenticação de produção
configurada nem amplia a exceção para acesso externo.
[Decisão local](../001-base-fiscal-versionada/adrs/0008-configuracao-local-modelos.md).

## Escopo

Api.Agente recebe o arquivo, identifica o layout, executa validadores determinísticos,
consulta o servidor Api.RAG por MCP e utiliza um modelo gerador OpenAI para explicar os
resultados e elaborar o parecer. Esta feature consome a base da feature 001; não duplica
ingestão, armazenamento vetorial, publicação documental ou credenciais do banco RAG.
A entrada externa será SOFIA/INFIS via A2A, especificada na Feature 003; REST de execução/leitura
permanece adaptador técnico da aplicação. Memória conversacional é incremento separado.

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

## Evidência histórica do incremento inicial

AG002/AG003/AG004/AG007/AG008 entregaram host/contratos/segurança e cliente MCP. Naquela etapa não havia
POST /api/v1/analises, identificação de leiaute suportado, regras fiscais e OpenAI gerador ou
persistência própria de análise/consumo. O diagnóstico administrativo indica explicitamente
analysisAvailable=false e fiscalReadiness=not_assessed.

Os cenários técnicos são comprovados com servidor MCP independente em outro processo,
snapshot 1.0, dados sintéticos e JWT de teste. Nenhum dado real, cobrança OpenAI ou alteração
na Api.RAG foi necessário. A revisão da interoperabilidade com a versão final da RAG em
trabalho concorrente permanece pendente. [Evidências](validation/initial-mcp.md).

## Incremento de leitura baseado no manual fornecido

Parser estrutural e POST /api/v1/sped/leitura implementados. O guia 1.35 de 18/06/2021
fundamenta delimitadores/Latin-1/CRLF e os campos iniciais/finais. A leitura preserva valores
brutos e informa COD_VER declarado, sem converter a edição do guia em versão do arquivo.
Nenhum leiaute é afirmado como integralmente suportado; fiscalValidationStatus permanece
not_performed e o status geral é partial para leitura sem ocorrências, failed se inválida.

Não há interpretação de tipos/campos específicos, hierarquia de blocos ou regras fiscais
neste incremento. AG005/AG006 permanecem parciais; AG001 continua pendente de política e
aprovação restante. [Fonte e limites](validation/supported-layouts.md),
[contrato](contracts/parser-sped.md) e [evidência](validation/parser-sped.md).

## Consumidora confirmada — SOFIA/INFIS

A solicitação inicial pertence à SOFIA; Api.Agente atua como especialista SPED quando for
selecionada. As histórias “solicitante” descrevem o usuário final mediado pela orquestradora,
não exigem interface conversacional nem roteamento multiagente neste serviço. Resultados
determinísticos, evidências e parecer são devolvidos à SOFIA para agregação separada.

A2A é especificado na [Feature 003](../003-integracao-sofia-a2a/spec.md); esta feature continua
responsável pelo núcleo de análise e pelo MCP, sem alterar Api.RAG. Endpoints REST existentes
não se tornam A2A por renomeio. Adaptadores REST/A2A devem compartilhar casos de uso, evitando
normalização do texto recebido, duplicação de regras ou calls HTTP internas. Contratos SOFIA,
REST interno e JSON do LLM são distintos. Nenhuma task AG foi concluída apenas pelo ajuste SOFIA.

## Incremento de análise/parecer e esclarecimentos atuais

O usuário delimitou períodos de 2020 a 2026; anos não são COD_VER. O mapeamento oficial de
códigos/vigências e o tipoDocumento da RAG não foram definidos/aprovados. O usuário fornecerá
prazos/referência da política e configurará a conexão PostgreSQL própria localmente.

POST /api/v1/analises e AnaliseSpedService agora coordenam leitura estrutural, datas do 0000,
mapeamento inequívoco parametrizado, busca/resolução MCP, OpenAI/Structured Outputs e validação
de JSON/citações/pendências. AG009/010 implementadas. Ledger próprio mede tentativas, inclusive
falhas/retries e uso ausente nulo; consulta é isolada e expurgo administrativo é limitado.
Arquivo, prompt e parecer não são persistidos. Custos estimados/informados são separados;
preço ausente continua nulo, sem cálculo tarifário presumido.

O exemplo real não contém perfis nem política aprovados. Sem política/conexão, análise falha
antes de dependências faturáveis; sem perfil, retorna unsupported. Com contexto sintético
mapeado e parecer válido, retorna partial e revisão humana, porque campos/hierarquia/regras
fiscais integrais não foram executados. Health/diagnóstico mantêm analysisAvailable=false
como ausência de prontidão fiscal/operacional, sem negar a existência do endpoint parametrizado.

125 testes aprovados cobrem consumidor MCP em processo separado, PostgreSQL descartável e
gerador simulado. Não há ensaio da versão final concorrente da RAG, modelo real/corpus ou
autorização de dados reais. AG001/005/006/011–014 continuam com pendências em tasks.md.
[Contrato](contracts/analysis-rest.md), [ADR](adrs/0003-fluxo-parecer-auditoria.md),
[evidências](validation/interoperability.md). A2A/Feature 003 permanece planejamento separado.

## Interface de testes solicitada

Swagger/OpenAPI da Api.Agente disponível somente em Development para executar os endpoints
REST reais. Upload usa seletor de arquivo Latin-1/CRLF e corpo bruto; autenticação segue o
esquema ativo, sem alterar policies ou a exceção loopback. UI respeita o guard de transporte;
não é publicada em Production. Não muda os gates fiscais/de dados ou implementa A2A.
AG015 concluída com quatro testes de contrato HTTP e regressão completa (129 aprovados).
[Guia](quickstart.md), [evidência](validation/swagger.md).
