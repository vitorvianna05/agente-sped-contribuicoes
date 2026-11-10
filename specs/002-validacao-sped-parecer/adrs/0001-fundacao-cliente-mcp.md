# ADR 0001 — Fundação da Api.Agente e cliente MCP

Data: 2026-11-10. Escopo autorizado: etapa 3, AG002/AG003/AG004/AG007/AG008.
Outro LLM ajusta a Api.RAG; este incremento não modifica seus fontes, testes, configuração,
contratos ou artefatos da Feature 001. A solução apenas recebe os dois novos projetos.

## Decisão

Api.Agente net10.0 tem projeto web e testes próprios. Controllers, Analise, Validacao, Mcp,
Llm, Contracts, Domain e Infrastructure tornam explícitas as responsabilidades; diretórios
não implementados têm README de escopo, sem validadores ou classes de negócio fictícios.
Não há ProjectReference, SQL, credencial de banco ou acesso a assemblies internos da RAG.

ConhecimentoMcpClient usa SDK ModelContextProtocol 2.3.0 já fixado no repositório, com
HttpClientTransport StreamableHttp explícito, CreateAsync, ListToolsAsync e CallToolAsync.
Não há fallback SSE/REST ou escolha de URL/tool pelo consumidor/LLM. Descoberta valida os
schemas completos das duas tools obrigatórias antes de chamar. O contrato público 1.0 foi
copiado como snapshot independente, com DTOs e validação local; não é vínculo de compilação.

Snapshot: src/Api.Agente/Mcp/Contracts/knowledge-v1.schema.json
SHA-256: a55b110da25b2873947034eefd51ebf53ce31d30cff2b89831c007e240feca0d
Origem: specs/001-base-fiscal-versionada/contracts/mcp-tools.schema.json no início da etapa.
Mudança no contrato da RAG exige confronto/revisão explícita do snapshot; falha de schema é
técnica, nunca permissão para consumir payload sem validação. Não garante compatibilidade
com mudanças concorrentes posteriores do outro LLM.

Responses textuais JSON são validadas estritamente. structuredContent, quando presente,
deve ser igual ao texto. IsError e technical_failure devem concordar; falha bloqueia uso
de evidência. Busca valida tipo/layout/estado ativo; resolução exige exatamente o referenceId
solicitado. Consulta sem evidência permanece limitação normal. Trechos continuam dados não
confiáveis; não são instruções executadas. Campos operacionais não são atribuídos pelo LLM.

## Segurança e limites

JWT normal exige emissor HTTPS, audience, assinatura, expiração e sub. Diagnóstico exige
scope analysis.diagnose e atribuição externa do principal; analysis.execute não concede
administração. Credencial MCP é de serviço, externa, não encaminhamento do token do usuário.
Não há corpo com ator, bearer, profileId, topK ou parâmetros arbitrários para a RAG.

A autorização anterior para ignorar autenticação local foi aplicada também à nova Api.Agente:
DisabledForDevelopment somente em Development loopback, ator development-local com permissões
locais. HTTPS e hosts continuam exigidos. A flag falha no startup em outros ambientes.
Sem bearer MCP só com AllowUnauthenticatedLoopback explícito e destino loopback em Development;
HTTP é permitido exclusivamente em Testing loopback para fixtures. Nenhum bypass de certificado
TLS é instalado. Redirecionamentos/cookies são desativados. P04 real permanece pendente.

Prazo global cobre conexão/descoberta/chamada; cancelamento do chamador é preservado. Não há
retry de aplicação, reconexão SSE ou GET stream autônomo; respostas são limitadas em bytes
antes da desserialização, mesmo sem Content-Length. Limites HTTP e correlationId/executionId
são atribuídos pelo sistema. Logs contêm somente ator/correlação/status; exceções de rede
não são expostas como detalhe, nem payloads/documentos/tokens.

## Endpoint e limites da entrega

GET /admin/v1/knowledge/readiness confere descoberta/schema; retorna apenas metadados,
analysisAvailable=false e fiscalReadiness=not_assessed. Não é endpoint funcional alternativo
de consulta de evidências, nem análise SPED. /health/live só indica processo vivo.

Contratos de metadados/estados/envelope e ParecerDraft são separados. Validação semântica
completa de citações é AG009/AG010. Não há POST /api/v1/analises (AG012), parser/regras fiscais
(AG005/AG006), geração OpenAI (AG010), persistência de consumo/auditoria própria (AG011),
memória ou A2A. Nenhuma cobrança de embeddings é recontabilizada no agente.

## Fontes técnicas consultadas e validação

SDK oficial: https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Client.HttpClientTransport.html
Opções: https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Client.HttpClientTransportOptions.html
As versões não foram atualizadas. Testes usam servidor MCP independente em outro processo,
Kestrel, schemas/DTOs sintéticos e JWT de teste; não usam banco/provedor da RAG ou OpenAI real.
Interoperabilidade com a versão final concorrente da Api.RAG deve ser verificada após sua
estabilização; não é alegada como validada por um servidor sintético. AG013 continua pendente.
