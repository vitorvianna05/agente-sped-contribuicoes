# Plano — Api.Agente consumidora MCP e parecer SPED

Desenho: 2026-11-05; sincronização: 2026-11-10. Estado: fundação/MCP/parecer/Swagger implementados; núcleo de análise parametrizado, 8 tasks concluídas e 7 com pendências. Referências: spec.md,
docs/architecture.md, feature 001 e constitution 1.1.0.

## Arquitetura

ASP.NET Core .NET 10; projeto Api.Agente e testes Api.Agente.Tests criados em AG002, com configuração
e lockfiles próprios; sem referência ao projeto ou banco da RAG. A restrição de dois projetos na feature 001 vale àquela entrega;
este novo serviço tem responsabilidade própria prevista na constitution, não é camada vazia.

SOFIA → adaptador A2A (Feature 003) ou REST técnico → AnaliseSpedService → validadores determinísticos → IConhecimentoClient/MCP →
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

OpenAiParecerLlm usa o SDK OpenAI 2.14.0 com gpt-4.1-mini da decisão mais recente (2026-11-10, antes gpt-4o-mini),
configurável externamente, timeout e tentativas finitas. AG014 ainda avaliará o modelo/corpus.
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
Não inferir preços, layouts ou critérios fiscais pelo exemplo da Api.Atendimento. O modelo
foi escolhido pelo usuário; a escolha não constitui avaliação aprovada.

## Ordem de execução e limites atuais

A Feature 001 já entrega T041/T043 em ambiente sintético; não é necessário aguardar todo o
aceite fiscal da RAG para criar AG002/AG003 e preparar AG007. AG008 ainda depende de AG004
e AG007; não ignorar os predecessores. AG013 depende também da matriz T049, ainda aberta.
AG006/AG011 e o aceite oficial dependem das entradas reais AG001 e gates correspondentes.

A Api.Agente aplica seus próprios controles de AG004, incluindo dispensa de JWT autorizada
somente em Development loopback; não herda credenciais da RAG. P04 real permanece aberto. Modelo padrão do parecer: gpt-4.1-mini, configurável externamente;
embeddings continuam no serviço RAG. Sem alteração de IDs, estados ou dependências das tasks.

## Incremento executado — etapa 3

AG002/AG003/AG004/AG007/AG008 implementadas. GET /admin/v1/knowledge/readiness exige
analysis.diagnose + principal atribuído no modo JWT; apenas descobre/confere schemas e não
retorna evidências. Health/live declara analysisAvailable=false. Parecer e endpoint de
análise continuam AG009–AG012. O cliente expõe busca/resolução para o caso de uso futuro.

Snapshot público MCP 1.0 e DTOs independentes; schemes completos confrontados a cada conexão,
JSON textual validado e structuredContent equivalente quando presente. Bearer de serviço
vem da configuração externa. HTTP/redirecionamentos e respostas ilimitadas não são fallback.
A exceção HTTP é somente Testing loopback; Development exige HTTPS mesmo sem JWT local.

Referências: [ADR 0001](adrs/0001-fundacao-cliente-mcp.md), [contratos](contracts/foundation.md),
[quickstart](quickstart.md) e [evidências](validation/initial-mcp.md). Testes MCP usam outro
processo com servidor sintético, sem alterar/iniciar a RAG em mudança por outro LLM. É uma
validação técnica do consumidor, não do SPED completo ou da versão final concorrente da RAG.

## Incremento — leitura estrutural SPED

ISpedParser/SpedParser leem bytes Latin-1 por stream com limites finitos/cancelamento;
SpedRecord/SpedDocument/SpedFileStructure preservam e verificam estrutura observável, sem
SDK/HTTP no domínio. Não se convertem campos numéricos/datas ou tabelas de versão por
suposição. POST /api/v1/sped/leitura exige analysis.execute e retorna leitura + limitações,
sem persistência, arquivo temporário, IA ou consulta MCP. O corpo bruto evita buffering
multipart no disco; arquivo é limitado também quando não há Content-Length.

O manual foi recebido como fonte de AG001; aprovação de regras/política e semântica de
leiautes permanecem pendentes. AG005/AG006 continuam abertas. Componentes/casos de teste
concretos e páginas de referência estão em [ADR 0002](adrs/0002-parser-sped-leitura.md) e
[validation/parser-sped.md](validation/parser-sped.md). Isso não implementa AG012/AG013.

## Entrada mediada pela SOFIA

SOFIA/INFIS recebe o usuário, documentos e pergunta e decide se chama Api.Agente. A aplicação
retorna a análise especializada; seleção de outros agentes, agregação e síntese final são da
SOFIA. A2A 0.3.x JSON-RPC/contrato SOFIA 1.0 e preservação de IDs estão na Feature 003.
AG012 deve oferecer caso de uso reutilizável por adaptadores, não endpoint como núcleo.
REST técnico permanece compatível com a constitution e com as ferramentas existentes.

A entrada SOFIA já é texto UTF-8 JSON; ISpedParser atual é bytes Latin-1. AA003 define porta
textual e proveniência, sem encoder com substituição, mudança de CRLF ou alegação de hash
original. AG012 não precisa do transporte A2A para testar o núcleo; AA006 reutiliza esse
núcleo quando pronto. Mantêm-se dependências e aceite fiscal da Feature 002.

## Incremento implementado — núcleo parametrizado

AnaliseSpedService utiliza as portas IParecerLlm/IAnalysisLedger definidas em Analise;
infraestrutura implementa OpenAI e PostgreSQL próprios. DTO público atual é AnalysisResult;
protótipos sem rota publicada da fundação foram removidos. Não há novo projeto/camada física,
Function Calling, memória ou seleção de tools pelo modelo. ADR 0003 registra limites e alternativas.

LayoutPolicy.Profiles estabelece código/período 2020–2026 → contexto RAG/fonte/aprovação,
sem tabela fiscal inventada. O domínio preserva resultados estruturais e estados coerentes;
SpedFileValidation/LayoutApplicability avaliam período/contexto sem IOptions/transportes;
SpedValidator adapta configuração e ParecerValidator confronta JSON/citações/pendências.
Resolução MCP compara proveniência/localização/texto/estado integralmente, exceto score.
Ausência de evidência bloqueia LLM; erro estrutural/sem mapeamento bloqueiam MCP e LLM.

MeasuredParecerLlm registra intenção antes de cada tentativa e uso conhecido mesmo se o
rascunho for inválido. SDK sem retry oculto; timeout/retry explícitos. Falha na persistência
impede repetição/publicação, mantém pending para reconciliação e não presume custo zero.
Schema agente tem execução e tentativas com política/expiração capturadas. Consulta por
subject + organization_id; migração e expurgo explícitos usam papel administrativo distinto,
sem DDL/DELETE no host. Expurgo em lote não remove retenção vigente nem pai com filhos.
Não persiste arquivo, prompt, evidência textual ou parecer. Cálculo tarifário permanece
incremento dependente de tabela de preços; campos de estimativa/faturamento são nulos.

Suite com REST/Kestrel, processo MCP sintético separado e PostgreSQL próprio descartável.
SDK OpenAI testado com HTTP simulado/Structured Outputs/uso ausente. Sem chamada faturável,
sem alterações na RAG ou no seu banco. Aceite integral preserva dependências AG001/006/011,
AG013 real/T049 e AG014. [Contrato](contracts/analysis-rest.md),
[evidência](validation/interoperability.md), [política pendente](validation/data-policy.md).

## Swagger para ensaios manuais

Incremento AG015 solicitado pelo usuário: Microsoft.AspNetCore.OpenApi 10.0.14 +
Swashbuckle.AspNetCore.SwaggerUI 10.3.1, versões centrais existentes, sem depender da Api.RAG.
Documento /openapi/v1.json e UI /swagger registrados somente em Development, depois do guard
de transporte. Upload documentado como corpo bruto application/octet-stream (string/binary),
com seletor do arquivo original, sem criar contrato multipart ou reencodar texto para UTF-8.
JWT descrito por operação autorizada; health permanece anônimo e modo local não exige JWT
fictício. Nenhuma credencial é embutida no documento. Testes HTTP verificam schema, interface,
transporte/ambiente e preservação de caracteres Latin-1 pelo contrato documentado.
