# Adaptadores MCP

BuscarConhecimentoTool e ResolverReferenciaTool delegam à aplicação via ConhecimentoService.
KnowledgeMcpTools registra os handlers oficiais do SDK, expande o catálogo versionado de schemas
e rejeita propriedades extras. Respostas usam structuredContent e cópia textual JSON equivalentes.
Cada execução exige knowledge.read; identidade e correlação vêm do transporte.
Ingestão administrativa é exposta por Controllers e exige knowledge.ingest; não é tool MCP.
