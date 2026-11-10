# Evidências — Phase 2: fundação

> **Nota (2026-11-10):** relatório histórico; IdempotencyCoordinator/RevisionPreconditions foram removidos pela [ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md).

Data: 2026-11-05. Escopo: T007–T018. Sem implantação, corpus fiscal ou chamadas reais de embeddings.

| Task | Entrega | Evidência principal |
| --- | --- | --- |
| T007 | Testes prévios das invariantes e contratos comuns | ConfigurationRulesTests, TransportEnvelopeTests; execução vermelha com 2 falhas comportamentais em 3 testes Unit antes da implementação. |
| T008 | Options, validação de limites/identidade/perfis/políticas e bloqueio de persistência | ConfigurationRulesTests e DomainFoundationTests.SyntheticPolicyDoesNotApproveOperationalUse. |
| T009 | DTOs e validação estrita de obrigatórios, nulos, extras, duplicatas e semântica de evidência | TransportEnvelopeTests. |
| T010 | Problem Details, códigos, correlação e logs sem mensagem/stack do erro original | ProblemDetailsTests e resposta 401 integrada. |
| T011 | JWT Bearer, scopes + atribuição central, hosts/origins/HTTPS e metadados do recurso MCP | FoundationEnforcesBearerAndCentralPermissionsWithAudit; tokens errados por issuer, audience, assinatura e expiração; administrador organizacional negado. |
| T012 | Portas pequenas e objetos de valor; TimeProvider para relógio | DomainFoundationTests.OfficialValuesPreserveTheirMeaning; domínio/aplicação sem referências de HTTP, banco ou SDK externo. |
| T013 | Npgsql/Pgvector, SQL versionado com checksum e comando --migrate | FoundationDatabase executa migrações duas vezes; ApplicationIdentityCannotMigrateOrCreateTables nega DDL pela identidade de aplicação e testa Program.Main em modo --migrate. |
| T014 | Operacao com campos completos e invariantes de estado/UTC/lease | OperationStateRequiresItsLifecycleFields verifica estados/UTC/lease sem infraestrutura; registro/leitura real em ConcurrentRegistrationIsAtomicAndScopedToPrincipal. |
| T015 | Constraint concorrente, replay, conflito de payload, ETag/If-Match | 8 registros concorrentes → uma operação e uma auditoria de aceitação; principal distinto não compartilha chave; DomainFoundationTests verifica 428/412. |
| T016 | Intenção/desfecho por tentativa, nulos, preço em decimal, retries e cancelamento | ConsumptionRecordsRetriesNullsPricesAndCancellation e TimeoutAndIncompleteConsumptionRemainExplicit. |
| T017 | Auditoria atômica de efeito, transporte e leitura central restrita | Rollback conjunto sem Commit; negação de leitura ao administrador organizacional; CancelledTransportStillWritesAudit comprova persistência com token já cancelado. |
| T018 | Composição em Program, grupo administrativo e MCP protegido sem tools fiscais | Consumidor SDK em processo separado descobre lista vazia autenticada na Api.RAG real; nenhum endpoint de consulta funcional REST criado. |

## Implementação e limites

- Somente Api.RAG e Api.RAG.Tests. Composição conhece adaptadores; domínio e aplicação não
  dependem de Npgsql, HTTP, SDK OpenAI ou MCP. Não há regras fiscais no transporte.
- Banco em contêiner PgVector descartável, com senhas aleatórias e identidade DML distinta da
  identidade de migração. Migrações não rodam na inicialização da API.
- Em testes, chave JWT efêmera, metadata OIDC estática e Data Protection efêmera; sem acesso
  a emissor real, tokens reais ou chaves do usuário. Cliente HTTP de testes bloqueia rede externa.
- Timeouts HTTP e limites de corpo vêm da configuração. Falhas de persistência impedem execução
  faturável; falha no registro posterior conserva pending/unknown. Nenhum dado fiscal/prompts
  é gravado nas tabelas de consumo/auditoria.
- O transporte prepara a resposta antes de registrar sua auditoria; efeitos de negócio futuros
  devem usar a mesma Unit of Work da auditoria de efeito. Um cancelamento não desfaz Commit.
- API sem configuração válida falha no startup; template Example não é carregado automaticamente.
  Sem conexão/política válida, pedidos funcionais não iniciam persistência ou execução dependente.
- PDF e OpenAI reais não são implementados nesta fase. Portas ficam prontas e o decorator medido
  foi testado com providers sintéticos; a composição desses adaptadores pertence a T027/T029.
- Ausência de perfil operacional continua erro PROFILE_INCOMPATIBLE. Nenhum perfil/modelo/topK
  ou prazo real foi aprovado. Gates P01–P04 continuam pendentes para operação real.

## Reprodução

```powershell
dotnet restore AgenteSpedContrib.slnx --configfile ./NuGet.Config --locked-mode
dotnet build AgenteSpedContrib.slnx --no-restore
dotnet test --project tests/Api.RAG.Tests/Api.RAG.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/Phase2-Acceptance
dotnet format AgenteSpedContrib.slnx --verify-no-changes --no-restore
```

Docker/engine Linux é necessário para Integration. Unit/Contract não usam provedor externo.
As saídas anteriores de testes que falharam na construção da fase permanecem em TestResults
(ignorado); a evidência versionável final é sanitizada sem alterar casos, contagens ou resultados.

## Migrações e configuração externa

Fornecer `ConnectionStrings__Rag` (usuário da aplicação) e `ConnectionStrings__RagMigration`
(usuário com DDL/CREATE EXTENSION no mesmo banco) via ambiente/secret manager, sem gravá-los no Git.

```powershell
dotnet run --project src/Api.RAG/Api.RAG.csproj --no-build --no-launch-profile -- --migrate
```

O comando termina após as migrações. Para iniciar o host, retirar `--migrate` e fornecer as
demais configurações exigidas pelo exemplo. Tokens devem ter audience da Api.RAG e `sub`;
scopes administrativos também precisam de atribuição em Authentication:CentralPermissionAssignments.
Authority e Resource são HTTPS; AllowedHosts e AllowedOrigins são listas explícitas.
Não adotar provider Fake ou ambiente Testing para processar dados operacionais reais.

Decisões e alternativas: [ADR-0003](../adrs/0003-fundacao-operacional.md).
## Resultado final

T007–T018 concluídas. Suíte final em `TestResults/Phase2-Acceptance-Final`: **32 aprovados,
0 falhas, 0 ignorados** (10 Unit, 15 Contract, 7 Integration), incluindo regressão da Phase 1.
Evidência versionável: [phase-2.trx](phase-2.trx), com usuário/máquina/caminhos locais sanitizados.
Build final: 0 avisos e 0 erros. Restore `--locked-mode` aprovado. Formatação validada.

O comando da suíte final foi o de reprodução acima com diretório de saída
`TestResults/Phase2-Acceptance-Final`. A execução `Phase2-Red` confirmou 2 falhas em 3
invariantes Unit antes da implementação; um filtro inicial inválido havia executado zero testes
antes de ser corrigido. Nenhuma execução com zero testes foi usada como aceite.
No desenvolvimento, GET /mcp recebeu 405, coerente com o transporte stateless escolhido;
o teste de segurança foi corrigido para POST. Listagem vazia precisou de handler explícito,
pois uma coleção vazia de tools isoladamente não anunciava tools/list no SDK selecionado.
Essas falhas foram corrigidas e os cenários passaram na execução final.

## Aceite após revisão de princípios

Os quatro achados foram corrigidos. Aceite atual: 41 testes aprovados, sem falhas ou ignorados; detalhes e evidência em [phase-2-review.md](phase-2-review.md). O resultado de 32 testes acima é histórico. O cálculo monetário passou para o domínio; a migração aditiva 0005 separa moedas e reforça a cronologia. Os gates operacionais permanecem pendentes.
