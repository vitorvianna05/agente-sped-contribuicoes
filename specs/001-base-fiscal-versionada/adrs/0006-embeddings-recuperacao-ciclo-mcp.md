# ADR 0006 — Embeddings, recuperação, ciclo de vida e MCP

> **Nota (2026-11-10):** as seções *Busca e referência* (catálogo/perfil adotado) e *Ciclo administrativo* (approve, reprocess, retirada, expurgo, corte de catálogo, replay) foram substituídas pela [ADR 0009](0009-simplificacao-ingestao-busca.md). Embeddings, MCP e contratos continuam vigentes; custo estimado passou a usar OpenAI:Pricing ([ADR 0010](0010-ambiente-local-openai-swagger.md)).

Data: 2026-11-05. Escopo autorizado: ajustes restantes 5–11 da API RAG.
Complementa ADR 0005; não altera a constitution nem aprova parâmetros produtivos.

## Decisão e organização

OpenAiEmbeddingService usa OpenAI 2.14.0/EmbeddingClient, modelo/dimensão do perfil e
Embeddings.ApiKey externo. GenerateEmbeddingsAsync com um texto por chamada preserva usage;
HttpClient é injetado e o SDK usa ClientRetryPolicy(0). O decorator existente registra cada
tentativa, incluindo erros e retries limitados. Valores monetários não informados continuam nulos;
não foi inventada tabela de preços. Tokens lexicais de chunking não são tokens de cobrança.
Referência: [API oficial de embeddings](https://developers.openai.com/api/reference/resources/embeddings/methods/create).

Após texto ready, EmbeddingWorker reivindica lease persistente próprio. Cada vetor validado é
salvo em transação curta; restart reutiliza checkpoints. Chamadas ambíguas sem checkpoint podem
repetir cobrança, sempre como nova tentativa auditada. O manifesto integral cobre hashes textuais
e vetores; falha não torna uma geração elegível. ProcessingAdmission compartilha a concorrência
entre extração e embeddings. MaxPreparationAttempts limita retomadas por estágio; shutdown
conserva lease e erro confirmado/timeouts têm desfechos explícitos. Não há transação durante rede.

KnowledgeRepository implementa portas por arquivos coesos: embeddings, recuperação, ciclo,
retenção e catálogo. Casos de uso aplicam autorização, domínio governa publicação/ativação,
vetores, retenção e completude do catálogo. Controllers e Tools somente traduzem transportes.
Não foram criados novos projetos, broker, banco ou fallback de provedor.

## Busca e referência

Busca exata PgVector por cosseno seleciona publicação, geração ativa, estado succeeded,
perfil/catalogo e aplicabilidade documentType/layoutVersion antes de limitar/retornar resultados.
Não adotar HNSW sem medição. Snapshot RepeatableRead impede compor perfil e ponteiros de estados
distintos. Configuração de perfil é comparada ao snapshot imutável antes da chamada de query;
UUID igual não autoriza trocar modelo ou dimensão. Distância/score não são confiança fiscal.

ResolverReferencia retorna o texto do UUID exato e nunca a geração nova. activated_at comprova
que a geração já foi publicada: nem candidato staged nem reprocessamento falho vira referência
histórica. Após expurgo, tombstone retorna REFERENCE_PURGED enquanto retido; após seu prazo,
REFERENCE_NOT_FOUND. Nenhuma referência desconhecida é redirecionada para texto parecido.

## Ciclo administrativo

Aprovação exige versão unpublished, geração integral succeeded, perfil adotado com evaluationId,
perfil operacional compatível e revisão. Em produção exige ProfileApprovalReference externo.
Registro de aprovação é um gate administrativo; qualidade não é inferida pelo servidor.
Reprocessamento usa os bytes originais e registra revisão/ponteiro esperados. Sucesso de fonte
published ativa integralmente se ainda compatível; retirada concorrente gera ACTIVATION_CONFLICT
e preserva o histórico. Uma fonte retirada não é restaurada. Replay usa resposta persistida,
evitando repetir efeitos ou devolver um estado posterior como se fosse a resposta original.

Retirada/substituição registram marco, política e vencimento imutáveis. Retenção de geração
considera também ReferenceDays. Expurgo exige permissão própria, ETag do artefato, política
aprovada da mesma versão, prazo vencido e ausência de dependência ativa. Original não é apagado
enquanto ainda há trechos retidos ou processamento dependente. Remover texto/vetor preserva
identidade e tombstone; não existe expurgo automático. Projeções de retenção não incluem conteúdo.

O caso técnico AdotarPerfilDoCatalogo exige knowledge.publish e ETag do catálogo. Compara todas
as versões publicadas com gerações completas do novo perfil e troca os ponteiros/catalogo em
uma transação. Ausência de qualquer geração impede o corte; não há endpoint público de migração.

## MCP e contratos

SDK MCP 2.3.0 mantém Streamable HTTP stateless. Duas classes de tools delegam aos casos de uso;
handlers WithListToolsHandler/WithCallToolHandler registram os schemas do arquivo versionado,
resolvendo refs locais. Essa alternativa oficial ao registro por atributos mantém nomes/campos
e additionalProperties=false sem inventar protocolo JSON-RPC.
[Documentação do SDK](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html).

Argumentos extras, tipos incorretos, tools desconhecidas e UUIDs inválidos são erros de protocolo;
contexto insuficiente é invalid_context. Sem evidência é insufficient_evidence. Perfil ausente,
falha de banco/provedor e timeout são technical_failure/isError=true. Respostas passam por
ContractValidator e têm structuredContent e cópia textual JSON iguais. Toda resposta declara
limitações. Cada execução e tentativa de embedding é auditada sem PDF/prompt/token de acesso.

## Validação e revisão futura

Testes usam provider sintético explícito ou HTTP simulado do SDK; a integração exercita o servidor
real e cliente MCP em processo separado. Não houve chamada OpenAI faturável ou migração real.
P01/P02/P03/P04 produtivos, corpus oficial, avaliação comparativa e capacidade 300 PDFs/5 consultas
não são aprovados por fixtures. Api.Agente, validação SPED e parecer generativo são feature 002.
