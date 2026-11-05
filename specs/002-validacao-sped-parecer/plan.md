# Plano — Api.Agente consumidora MCP e parecer SPED

Data: 2026-11-05. Estado: planejamento; sem implementação. Referências: spec.md,
docs/architecture.md, feature 001 e constitution 1.1.0.

## Arquitetura

ASP.NET Core .NET 10; projeto Api.Agente e testes Api.Agente.Tests serão criados quando as
tasks forem executadas. A restrição de dois projetos na feature 001 vale àquela entrega;
este novo serviço tem responsabilidade própria prevista na constitution, não é camada vazia.

Controllers → AnaliseSpedService → validadores determinísticos → IConhecimentoClient/MCP →
IParecerLlm/OpenAI → validador da saída → envelope. Domain concentra invariantes; adaptadores
externos implementam portas. Program compõe DI, HttpClient, configuração e policies.

Estrutura funcional acompanha a referência: Controllers, Analise, Validacao, Mcp, Llm,
Contracts, Domain e Infrastructure. Não reutilizar modelos internos/banco da Api.RAG por
ProjectReference; consumir os contratos versionados de transporte.

## MCP

ConhecimentoMcpClient cria HttpClientTransport e McpClient, descobre/lista e chama
BuscarConhecimento com `{consulta,tipoDocumento,versaoLayout}`; ResolverReferencia recebe
referenceId. Timeout/cancelamento/autorização são explícitos. Resultado IsError=true ou
schema/semântica inválida interrompe o uso de evidência. O texto JSON de TextContentBlock
serve a compatibilidade do padrão de cliente; structuredContent é validado quando presente
e os dois não podem divergir. Nunca aceitar texto livre como prova de validação fiscal.

Integração sintética depende de feature 001 T043; testar falhas após T049. Operação real
depende de P01/P04/P02 da RAG e da política própria de arquivos/resultados da Api.Agente.
Só iniciar avaliação oficial depois de fechar esses gates.

## IA e dados

OpenAiParecerLlm usa o SDK OpenAI com modelo gerador externo, timeout e tentativas finitas.
Embeddings continuam na Api.RAG; a Api.Agente não mistura embeddings com geração de texto.
Prompt versionado separa instruções, resultados determinísticos e trechos não confiáveis.
Somente os campos fiscais necessários deixam o serviço, conforme política aprovada.

Function Calling, se usado, é integrado por Semantic Kernel; nenhuma seleção de tool pelo
modelo concede acesso ou pula validações obrigatórias. O fluxo principal é coordenado pela
aplicação, no padrão de serviço consumidor da referência, sem fallback de modelo presumido.

Api.Agente possui consumo/auditoria próprios no PostgreSQL, com identidade segregada. Registra
suas chamadas generativas; Api.RAG registra embeddings da consulta/documento. Consolidação
usa correlação e contratos autorizados, sem acesso ao banco alheio nem dupla contagem.
Não criar memória conversacional, Redis, MongoDB ou outro banco sem caso de uso/ADR específico.

## Testes e gates

- Unit: parsers/regras e confronto de citações sem HTTP, SQL ou LLM real.
- Contract: entrada, JSON do LLM, envelope público e schemas MCP.
- Integration: cliente SDK real em processo separado, RAG com fixture publicada e tokens sintéticos;
  provider LLM fake para medir preservação dos resultados e tratamento de falhas.
- Evaluation: corpus e rubrica fiscal aprovados, modelos/parâmetros comparados, custo/latência e
  referências verificadas; uma resposta isolada não comprova qualidade.

Versionar prompts/contratos, registrar evidências, executar build/format/testes pertinentes.
Não fixar modelos, preços, layouts ou critérios fiscais pelo exemplo da Api.Atendimento.
