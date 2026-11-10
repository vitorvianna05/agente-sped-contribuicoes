# ADR-0004 — Adoção do padrão funcional Api.Conhecimento

> **Nota (2026-11-10):** a *publicação explícita* desta decisão foi substituída por publicação automática ([ADR 0009](0009-simplificacao-ingestao-busca.md)). O padrão funcional e as tools MCP continuam vigentes.

Data: 2026-11-05. Estado: desenho autorizado, implementação pendente nas tasks abertas.
Origem: usuário indicou CorporativoAI.sln e confirmou IA generativa para interpretar os
resultados das validações e elaborar parecer com evidências.

## Decisão

Seguir a organização funcional da referência: Controllers/Ingestao/Rag/Tools no servidor;
Mcp/ConhecimentoMcpClient e serviço de análise no consumidor. Api.RAG continua proprietária
da base vetorial e Api.Agente fica responsável pelos validadores SPED e pelo parecer OpenAI.
São dois usos de IA: embeddings de trechos/consultas na RAG e geração de parecer no agente.

Ingestão planejada em POST /api/v1/ingest, com arquivo + metadata fiscal. Manter operação
durável, acompanhamento, publicação explícita e referências imutáveis. A referência usa 201
síncrono e substituição destrutiva; esses comportamentos não são apropriados às decisões
fiscais já aprovadas e não serão copiados.

Tools planejadas BuscarConhecimento e ResolverReferencia, transporte HTTP MCP em /mcp.
BuscarConhecimento exige consulta/tipoDocumento/versaoLayout, pois o contexto fiscal não
pode ser inferido por similaridade. O cliente da referência precisa de adaptação desses
argumentos e da leitura do Result JSON. Fornecer structuredContent e cópia JSON textual;
manter IDs, limitações e semântica de erro. Os nomes anteriores nunca foram registrados
na fundação; não há cliente operacional conhecido que precise receber alias legado.

Embeddings OpenAI + PgVector/cosseno são obrigatórios na operação. Modelos, dimensões,
topK/limiar e indexação dependem de avaliação. Não migrar do SDK MCP 2.3.0 para o 2.2.0
observado na referência sem justificativa. Não incorporar fallback Gemini, SQL Server,
usuário livre do corpo ou inferência fiscal do atendimento genérico.

## Limites e consequências

DDD e Clean Code permanecem: controllers/tools não contêm regras ou SQL; serviços coordenam
casos de uso; domínio preserva invariantes; adaptadores implementam portas. Não duplicar
portas/configurações existentes para reproduzir nomes da referência. Não há mudança de
constitution: ela já separa Api.RAG/Api.Agente e prevê geração de análise depois do MCP.

Feature 001 mantém a RAG sem gerador de parecer; feature 002 planeja o consumidor e a
interpretação. A tarefa de validar SPED exige fontes/regras oficiais e não passa a ser um
chat genérico. Memória/A2A serão incrementos próprios. Critérios de 300 PDFs/5 consultas/5s
referem-se somente à recuperação; parecer precisa de avaliação e SLA próprios.

T001–T018 e suas evidências são históricos. Não se marcou tarefa aberta como concluída,
não se moveu código e não se executou o projeto externo. Os contratos nesta revisão são
alvo de implementação; revalidar interoperabilidade em T041–T043 e AG007–AG013.
P01–P04 continuam pendentes para operação real.

## Evidências e alternativas

Arquivos locais efetivamente inspecionados e diferenças: docs/reference-api-conhecimento.md.
Alternativa de copiar integralmente a referência foi rejeitada por perder versão fiscal,
aprovação e referências estruturadas. Alternativa de manter todo o planejamento anterior
sem cliente/parecer concreto não atenderia ao esclarecimento do usuário. Adotou-se o padrão
de composição e comunicação, com adaptações fiscais explicitadas nos contratos.
