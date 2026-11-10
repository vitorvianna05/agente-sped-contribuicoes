# Adaptadores RAG

OpenAiEmbeddingService implementa IEmbeddingProvider com o SDK fixado, transporte HttpClient
e retries internos desativados; a chave vem de `OpenAI:ApiKey` (ou `Embeddings:ApiKey`).
ProfileEmbeddingProviderFactory aplica o decorator medido e o preço de `OpenAI:Pricing`.
ConhecimentoService adapta os resultados dos casos de uso ao contrato de saída MCP.
Busca exata PgVector e resolução estão em Infrastructure/Knowledge/Persistence/KnowledgeRetrieval.cs,
com o perfil configurado (sem catálogo); aplicação depende de IKnowledgeStore. Não existe busca
REST funcional ou geração de parecer aqui.
Decisões: specs/001-base-fiscal-versionada/adrs/0006, 0009 e 0010.
