# ADR 0003 — análise estrutural, MCP, parecer e auditoria próprios

Data: 2026-11-10. Estado: implementação técnica aceita para ensaio sintético; aceite fiscal e operação real pendentes.

## Contexto

O usuário autorizou implementar as tarefas faltantes entre Api.Agente e Api.RAG, preservando
a RAG em trabalho concorrente. Confirmou o período desejado de 2020 a 2026. Anos não são
valores literais de COD_VER; a edição 1.35 do guia não comprova compatibilidade integral.
O tipoDocumento publicado na RAG não está definido. O usuário fornecerá prazos/referência
da aprovação e configurará localmente a conexão PostgreSQL própria.

## Decisão

- AnaliseSpedService coordena parser → identificação/contexto → MCP → resolução exata de
  todas as referências → geração → validação do JSON/citações. REST é adaptador; não há A2A
  neste incremento. O fluxo é obrigatório e determinístico: não usa Function Calling.
- LayoutPolicy.Profiles mapeia COD_VER + intervalo inclusivo dentro de 2020–2026 para
  DocumentType/KnowledgeLayoutVersion, fonte e aprovação. Sem correspondência única, retorna
  unsupported antes da consulta. O exemplo real fica vazio; o código 006 nos testes é
  exclusivamente sintético, não um catálogo fiscal aprovado.
- Regras entregues verificam leitura/envelope/contagem e datas declaradas (campos 06/07 do
  0000, guia p.66). Campos específicos, hierarquia de blocos e regras fiscais continuam
  não realizados. Mesmo com parecer, o resultado é partial e exige revisão humana.
  SpedFileValidation/LayoutApplicability concentram regras puras de período/mapeamento no
  domínio; SpedValidator adapta opções externas, sem SDK/HTTP/SQL no domínio.
- OpenAiParecerLlm usa OpenAI 2.14.0, gpt-4.1-mini configurável (decisão mais recente na
  ADR 0010 da Feature 001, confirmado pelo usuário nesta sessão; anteriormente gpt-4o-mini)
  e Structured Outputs estrito.
  Prompt/schema v1 são recursos versionados. Apenas resultados estruturais mínimos e
  evidências são enviados; arquivo completo, nomes e CNPJ do upload não são enviados.
  Documento/evidência são dados não confiáveis. A correspondência de citações é verificável;
  a qualidade/verdade da prosa exige avaliação AG014, não é provada por um schema JSON.
- A saída do modelo não contém metadados operacionais. ParecerValidator exige referências
  recuperadas, preserva os códigos determinísticos de inconsistência e todas as pendências,
  rejeita propriedades extras/duplicadas e revisão humana falsa. AnalysisResult valida o
  envelope efetivamente publicado. O protótipo interno AnalysisRequest/AnalysisEnvelope da
  fundação foi substituído pelo contrato bruto/AnalysisResult antes de publicação funcional;
  nenhuma rota multipart ou consumidor externo havia sido entregue.
- PostgreSQL próprio, schema agente e papéis separados. Antes de cada chamada registra-se
  intenção pending; depois, estado e tokens efetivamente informados, inclusive JSON inválido,
  recusa e retry. Falha ao persistir conclusão mantém intenção pendente e impede nova chamada
  automática. Falha de auditoria bloqueia publicação do parecer. Não é uma outbox durável de
  respostas: uma queda pode deixar estado desconhecido que exige reconciliação operacional.
- Modelo/prompt/timestamps/status ficam por tentativa; execution_id liga sujeito/organização
  e correlation_id. knowledge_calls mantém operação, correlação remota e IDs das referências
  de respostas MCP válidas, inclusive erro remoto com ID disponível. Não se exige que a RAG
  repita o ID local; falha sem resposta válida não inventa ID. Não se guardam arquivo, prompt,
  texto das evidências ou parecer no banco.
  Custos estimados e informados têm campos/moedas separados e ficam NULL sem informação de
  preço/provedor. Não há tabela de preços presumida nem total monetário fictício. Embeddings
  continuam sendo medidos exclusivamente pela RAG; a correlação não copia seu consumo.
- AgentData requer conexão, PolicyVersion, ApprovalReference e prazos positivos para iniciar
  análise. Os prazos de auditoria devem cobrir os de consumo, pois preservam a identidade e
  correlação das tentativas. Expiração capturada na gravação não é recalculada por mudança de
  configuração. Expurgo explícito --purge-agent usa identidade administrativa e lote finito;
  o papel HTTP não recebe DELETE/DDL. Não há agendamento automático criado.

## Alternativas e consequências

Reutilizar banco/DTOs internos da RAG ou substituir MCP por REST violaria o limite entre
serviços. SDK MCP 2.3.0 e snapshot 1.0 são preservados. Copiar todo o SPED ao provedor ou
presumir um tipo documental inviabilizaria minimização/contexto fiscal. O mapeamento externo
permite avançar tecnicamente sem fabricar regras; dados reais continuam bloqueados pelos
insumos ausentes. Falha fechada de auditoria sacrifica disponibilidade para evitar chamadas
sem intenção rastreável. Retry do SDK é zero; retry explícito é limitado e auditado.

## Verificação e revisão

Testes unitários, SDK OpenAI com HTTP simulado, REST/Kestrel, servidor MCP sintético em
outro processo e PostgreSQL descartável. Não há chamada OpenAI faturável, migração real ou
ensaio da versão final concorrente da Api.RAG. AG001/005/006/011–014 têm pendências
explicitadas em tasks.md; operação real precisa dos insumos do usuário, integração das
APIs estabilizadas e avaliação aprovada. Rever o desenho se os prazos reais não permitirem
reter a correlação por todo o consumo, se for necessária retenção de resultados ou se
for aprovada tabela de preços/cálculo monetário.

Referência técnica consultada: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
e [GPT-4.1 mini](https://developers.openai.com/api/docs/models/gpt-4.1-mini), documentação oficial
OpenAI, 2026-11-10. Compatibilidade documentada não substitui ensaio real nem avaliação fiscal.
