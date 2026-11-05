# T005 — Infraestrutura isolada de testes

Data: 2026-11-05. Escopo: preparação da infraestrutura da Phase 1, sem endpoints fiscais.

## Componentes

| Componente | Implementação e limite |
| --- | --- |
| Host | `RagTestHost`: WebApplicationFactory da Api.RAG em Testing; limpa fontes de configuração e carrega apenas synthetic-settings.json e overrides explícitos. Conexão opcional deve vir de PostgresFixture; nenhuma conexão de usuário é descoberta. |
| Rede determinística | HttpClientFactory usa handler que rejeita saída HTTP. O provider sintético não usa rede. Chamadas diretas a HttpClient/SDK fora da factory não são interceptadas; futuros adaptadores devem usar as portas/dublês previstos. |
| Relógio | ControlledTimeProvider controla GetUtcNow e avanço explícito. Não simula timers/Task.Delay; futuras regras devem receber TimeProvider. |
| Embeddings | SyntheticEmbeddingProvider retorna vetor SHA-256 determinístico de três dimensões, conta chamadas e respeita cancelamento. É dublê técnico; não representa relevância fiscal ou o provedor OpenAI. T012/T029 definirão a porta/adaptador real. |
| PostgreSQL/PgVector | PostgresFixture cria contêiner descartável, banco sintético, senha aleatória por instância e porta dinâmica; habilita vector e descarta via DisposeAsync/Resource Reaper. Não reutiliza banco ou credenciais reais, não monta volumes do workspace. |
| Consumidor MCP | McpConsumerProcess inicia o próprio assembly de testes via dotnet em processo separado, sem projeto extra. Modo --mcp-consumer lê pedido JSON pelo stdin, usa SDK MCP 2.3.0 Streamable HTTP, lista tools e opcionalmente chama uma tool; devolve resultado e PID por stdout. |
| Limites do subprocesso | Somente HTTP loopback sintético; ambiente herdado limitado a PATH/SystemRoot/WINDIR/TEMP/TMP/DOTNET_ROOT, sem tokens ou conexões. Cliente com conexão 5s, execução 20s e pai 30s por padrão (ajustável no teste), cancelamento e encerramento da árvore em falha/timeout. Erro do filho informa apenas tipo da exceção. |
| Servidor de prova | SyntheticMcpServer usa Kestrel loopback com porta dinâmica, SDK oficial stateless e tools synthetic_echo e synthetic_wait (espera controlada para cancelamento/timeout). Está somente no projeto de testes. Não registra tools fiscais, autenticação real ou transporte na Api.RAG. |
| Categorias | TestCategories define Unit, Contract, Integration, Evaluation e Capacity. As últimas duas não têm testes ainda e continuam dependentes de suas tarefas e gates. |

Imagem do banco fixada por digest imutável, identificada no cache local da imagem oficial
pgvector/pgvector:pg17:
`pgvector/pgvector@sha256:ac08538c6f8b9904c33c8224c5e5706dbe760aca29db1d096972b4052c22a75d`.
Não se presume versão da extensão pela tag; o ensaio exige extensão criada e round-trip de vector.
Referências: [Testcontainers PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/),
[SDK MCP — transportes](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html).
As APIs foram conferidas também nos XMLs dos pacotes 2.3.0 instalados.

## Reprodução

Docker Desktop/engine Linux precisa estar em execução para Integration; ausência causa falha
explícita, sem converter o teste em sucesso/skip. Unit e Contract dispensam Docker/provedores
externos; Contract abre apenas socket loopback e subprocesso .NET. Não executar build sobre
o mesmo diretório bin enquanto um teste estiver em execução no Windows.

```powershell
dotnet restore AgenteSpedContrib.slnx --configfile ./NuGet.Config --locked-mode
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-build --no-restore --filter-trait Category=Unit
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-build --no-restore --filter-trait Category=Contract
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-build --no-restore --filter-trait Category=Integration
```

O entry point próprio mantém os caminhos do runner xUnit/MTP e reserva --mcp-consumer
somente para o teste. Não altera Program da Api.RAG nem adiciona serviço de produção.

## Rastreabilidade

| Critério T005 / plan Testing | Teste |
| --- | --- |
| Host isolado, relógio e bloqueio HTTP | HostUsesOnlySyntheticConfigurationAndBlocksOutboundHttp |
| Provider fake determinístico/cancelável | SyntheticEmbeddingsAreRepeatableAndCancellable |
| Consumidor real em outro processo, descoberta e chamada | McpConsumerDiscoversAndCallsAcrossProcessBoundary |
| Falha explícita sem alvo externo | McpConsumerRejectsNonLoopbackTarget |
| PostgreSQL/PgVector isolado | PostgresContainerSupportsVectorRoundTrip |

Esta infraestrutura não comprova as tools fiscais, segurança operacional P04, qualidade
RAG, capacidade SC-009/010 ou gates P01–P03. Esses critérios permanecem nas tarefas próprias.

## Resultado final T005

Comando executado em 2026-11-05:

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/T005-Final
```

Aprovado: 6 testes (3 Unit incluindo o teste T004, 2 Contract, 1 Integration),
0 falhas, 0 ignorados. A execução compilou a versão final antes de rodar.
Evidência: [t005-infrastructure.trx](t005-infrastructure.trx).

Docker inicialmente estava parado; foi iniciado e disponibilizou engine 29.8.2.
O primeiro ensaio do banco com tag 0.8.1-pg17 passou. Para reprodução imutável,
a versão final usa o digest documentado e também passou no ensaio completo.
A primeira asserção MCP esperava isError=false explícito; o SDK omite isError em
sucesso, como permitido pelo protocolo, então o teste rejeita apenas true e verifica
o conteúdo retornado. Uma tentativa de recompilar enquanto Integration rodava
falhou por bloqueio do executável no Windows; a execução final ocorreu após seu término.
Não há falhas pendentes desses ensaios.

Build final: 0 avisos, 0 erros. dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore aprovado (exit 0).
## Correções da revisão de princípios — 2026-11-05

requirements.md atualizado para os esclarecimentos e SC-001–010; tasks.md e quickstart.md
normalizados para LF, com verificação explícita das terminações e newline final.

Adicionados dois casos de McpConsumerTerminatesPendingProcess: cancelamento pelo chamador
e vencimento do timeout do pai. Ambos aguardam a entrada efetiva na tool synthetic_wait,
confirmam que o processo está vivo durante a chamada, verificam OperationCanceledException
e confirmam HasExited antes de liberar a tool/servidor. Não usam sleeps para sincronização.
O caso de timeout também comprova que o token do chamador não foi cancelado.
O consumidor aguarda os leitores de stdout/stderr no descarte, inclusive após encerrar o filho.

Executado:

```powershell
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --filter-trait Category=Contract --report-xunit-trx --results-directory TestResults/T005-Review
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

Resultado: 4 Contract aprovados, 0 falhas, 0 ignorados; compilação efetuada pelo comando de testes.
Formatação aprovada (exit 0). Evidência: [t005-review-contract.trx](t005-review-contract.trx).
O resultado de seis testes acima é o ensaio histórico completo da T005; esta revisão testa
os caminhos MCP alterados. Não foi repetido o teste de contêiner PostgreSQL, cujo código não mudou.

Regressão Unit executada com --no-build --no-restore --filter-trait Category=Unit: 3 aprovados, 0 falhas, 0 ignorados. Evidência: [t005-review-unit.trx](t005-review-unit.trx). Total nesta revisão: 7 testes aprovados.
