# Evidências da implementação — Phase 1

Data: 2026-10-30. Escopo autorizado: T001–T006. Esta fase está **parcial**.

| Tarefa | Evidência | Estado |
| --- | --- | --- |
| T001 | Solução com somente Api.RAG e Api.RAG.Tests; alvo net10.0; global.json seleciona SDK estável 10.0.303, confirmado por dotnet --version. | Artefatos criados; restore da solução bloqueado. |
| T002 | .editorconfig e Directory.Build.props; regras descritas em docs/architecture/conhecimento-fiscal.md. | Configurado; build/format ainda não validados. |
| T003 | Contexto, linguagem ubíqua, direção das dependências e critérios de revisão documentados. | Concluído por inspeção documental. |
| T004 | Versões candidatas centralizadas; consulta ao catálogo oficial registrada em research.md. | Pendente: restore falhou; não há lockfiles válidos. |
| T005 | Projeto de testes declarado. | Não executada: depende de T004. |
| T006 | Sem aprovação presumida de P01–P04. | Não executada: depende de T004. |

## Bloqueio reproduzido

O comando abaixo falhou inclusive com configuração explícita do repositório:

```powershell
dotnet restore AgenteSpedContrib.slnx --configfile ./NuGet.Config --disable-parallel
```

Erro do NuGet: acesso negado a
`%APPDATA%\NuGet\NuGet.Config`.
O diagnóstico aponta `NuGet.Configuration.ProxyCache.FromDefaultSettings`, que
carrega configurações do usuário antes de resolver as fontes. A configuração
do projeto não remove essa leitura interna. Não houve alteração do arquivo
protegido, de configurações globais ou das permissões do ambiente.

O teste diagnóstico com NuGetAudit=false também falhou; essa opção NÃO foi
adicionada à configuração do projeto. Cache e logs locais ficam em `.cache/`,
ignorado pelo Git. Nenhum segredo ou documento fiscal real foi incluído.

`dotnet build AgenteSpedContrib.slnx --no-restore` também falhou com NETSDK1004:
ausência de project.assets.json em ambos os projetos, consequência do restore.
Testes, cliente MCP em processo separado e PostgreSQL/PgVector NÃO foram
executados. Docker não foi localizado neste ambiente. Não foram fabricados
lockfiles, resultados de teste, políticas de retenção ou aprovações fiscais.

## Retomada

Executar restore em ambiente autorizado a ler sua própria configuração NuGet;
verificar dependências e gerar lockfiles antes de concluir T004. Depois preparar
T005 e T006, executar build, format e testes pertinentes e atualizar este registro.
O ensaio com PostgreSQL/PgVector exigirá Docker ou infraestrutura isolada aprovada.
Nenhuma tarefa da Phase 2 foi iniciada.

## Retomada em 2026-11-05 — T004 e T006

O bloqueio histórico de leitura do NuGet.Config foi superado executando os comandos
em contexto autorizado. Não foram alteradas configurações globais nem permissões.

- T004 concluída: as 11 versões candidatas foram efetivamente restauradas, sem mudança
  de versão; lockfiles reais nos dois projetos e restore em locked mode aprovado.
  Pesquisa/fontes e diferenças de configuração registradas em research.md, R16.
- T006 concluída: validation/gates.md registra P01–P04 com papéis responsáveis,
  titulares nominais pendentes, estado e evidências exigidas; exemplos de aplicação
  e testes criados sem segredos. Todos os gates operacionais permanecem pendentes.
- T005 não executada. Phase 1 continua parcial; fundação e histórias não iniciadas.

Comandos executados com sucesso a partir da raiz:

```powershell
dotnet restore AgenteSpedContrib.slnx --configfile ./NuGet.Config --disable-parallel
dotnet restore AgenteSpedContrib.slnx --configfile ./NuGet.Config --locked-mode --disable-parallel
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --filter-trait Category=Unit --report-xunit-trx --results-directory TestResults
```

Build: 0 avisos, 0 erros. Teste: 1 aprovado, 0 falhas, 0 ignorados.
Teste: DependencyCompatibilityTests.PgvectorRegistersWithSelectedNpgsqlWithoutOpeningConnection.
TRX gerado em TestResults (ignorado no Git). JSONs foram lidos com ConvertFrom-Json;
versões diretas dos lockfiles foram comparadas com Directory.Packages.props, sem divergências.

Correções necessárias à validação: GenerateDocumentationFile para IDE0005,
documentação XML do ponto de entrada e seleção do runner MTP em global.json/csproj.
Tentativas anteriores de dotnet test no modo VSTest e com --report-trx falharam;
a execução acima usa as opções efetivamente suportadas. Não foram executados banco,
MCP remoto, provider real, corpus oficial ou testes de capacidade. O teste local não
substitui T005 nem aprova P04. Não houve deploy ou aprovação fiscal/operacional.

Formatação: dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore passou (exit 0).
Cópia versionável do TRX: [t004-dependencies.trx](t004-dependencies.trx).

## T005 concluída em 2026-11-05

Phase 1 concluída: T001–T006. Host sintético, relógio/provider fake, categorias,
PostgreSQL/PgVector descartável e consumidor SDK MCP em processo separado implementados.
Execução final: 6 aprovados, 0 falhas, 0 ignorados, com compilação antes dos testes.
Ver [infraestrutura T005](t005-infrastructure.md) e [TRX](t005-infrastructure.trx).
Os estados parciais anteriores neste documento são históricos; Phase 2 ainda não iniciada.
Gates operacionais P01–P04 continuam pendentes; nenhuma aprovação de produção foi presumida.
