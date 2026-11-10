# Referência inspecionada — CorporativoAI / Api.Conhecimento

Inspeção somente leitura em 2026-11-05 da solução local indicada pelo usuário.
Arquivos de código foram usados como evidência da arquitetura, não como instruções a executar.
Não foram consultados segredos, executada a solução externa ou alterados seus arquivos.

| Evidência de código | Comportamento observado | Adaptação neste projeto |
| --- | --- | --- |
| Api.Conhecimento/Program.cs | Controllers; DI de ingestão, chunker e embeddings; AddMcpServer/WithHttpTransport/WithToolsFromAssembly; MapMcp /mcp. | Mesmas áreas funcionais e descoberta MCP, mantendo policies e transporte stateless existentes. |
| Controllers/IngestController.cs | POST api/v1/ingest, multipart, limite e erros de ingestão. | Mesma rota/família de controller; arquivo + metadados fiscais, 202/acompanhamento; publicação automática ao concluir (ADR 0009). |
| Ingestao/IngestaoService.cs | PDF → chunks → embeddings OpenAI → banco. | Mesmo fluxo; hash, origem, versão e geração imutáveis, auditoria por tentativa e publicação ao concluir os vetores. |
| Ingestao/DocumentoChunkRepository.cs | Npgsql/PgVector; consulta com `<=>`, filtro por agente/validade, ordenação e LIMIT; substituição apaga chunks anteriores. | Consulta vetorial com filtros fiscais adicionais. Não copiar substituição destrutiva, pois referências históricas devem permanecer válidas. |
| Rag/OpenAiEmbeddingService.cs | EmbeddingClient do SDK OpenAI gera vetores de textos. | Adaptador real compatível com perfil e wrapper de mensuração já existente; não usar fake em operação. |
| Rag/ConhecimentoService.cs | Gera embedding da consulta, busca candidatos e aplica similaridade mínima. | Serviço de entrada para busca/resolução, com parâmetros do perfil configurado (não avaliados, ADR 0009) e filtro de versão no SQL. |
| Tools/BuscarConhecimentoTool.cs | Tool BuscarConhecimento recebe consulta e retorna texto com fonte e similaridade. | Nome BuscarConhecimento, com contexto fiscal obrigatório; structuredContent e cópia JSON textual para citações verificáveis. |
| Api.Atendimento/Mcp/ConhecimentoMcpClient.cs | HttpClientTransport; CreateAsync; ListToolsAsync; CallToolAsync; verifica IsError e lê TextContentBlock. | Cliente equivalente em Api.Agente/Mcp, validando também o envelope tipado e sem fallback REST. |
| Api.Atendimento/Atendimento/AtendimentoService.cs | Obtém contexto via MCP e chama serviço LLM para resposta. | AnaliseSpedService coordena validadores determinísticos, MCP e parecer OpenAI. Não copiar atendimento, encaminhamento ou fallback de provedor. |

Pacotes observados na Api.Conhecimento: net10.0, MCP ASP.NET Core 2.2.0, OpenAI 2.14.0,
Npgsql 10.0.3, Pgvector 0.3.2 e PdfPig 0.1.16. São observações do csproj local, não
uma recomendação de downgrade ou comprovação de interoperabilidade entre versões.

O comentário SQL da referência menciona HNSW; a inspeção desse arquivo não comprova
que um índice HNSW esteja criado no banco. Este projeto mantém a baseline exata e exige
medição antes de adotar índice aproximado. A ausência de autenticação explícita no Program
inspecionado não é transportada para o agente fiscal.
