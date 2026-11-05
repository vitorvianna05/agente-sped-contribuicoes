# ADR-0003 — Fundação de contratos, identidade e persistência

Data: 2026-11-05. Escopo: T007–T018, QR-001–003, FR-009/010/015–019.
Estado: implementado e validado em ambiente sintético; não aprova gates P01–P04.
ADR-0002 continua reservado ao perfil avaliado de T077.

## Decisões

1. Manter dois projetos e camadas lógicas. Program compõe adaptadores; aplicação usa portas
   e domínio independente de HTTP, SQL e SDKs. TimeProvider é a porta de relógio da plataforma;
   não criar outra abstração equivalente. Portas PDF/embedding não recebem implementação fake
   em produção: adaptadores reais continuam em T027/T029. O decorator de consumo recebe uma
   implementação que representa exatamente uma tentativa física, com retries internos desligados.
2. JWT Bearer oficial 10.0.14 valida assinatura, issuer, audience e validade. `sub` é obrigatório;
   identidade nunca vem do corpo. Permissões administrativas exigem scope no token e atribuição
   central externa por principal. Papéis organizacionais não conferem privilégios centrais.
   Alternativa de papel administrador genérico foi rejeitada por FR-015. O emissor real permanece
   externo, sem selecionar provedor de identidade em nome do usuário.
3. Configurações são validadas no startup. Ausência de retenção não vira prazo zero ou infinito;
   ausência de perfil adotado não vira resultado sem evidência. Persistência auditável exige política,
   prazos de auditoria/consultas/consumo e aprovação P01 fora do ambiente sintético. Fake só é aceito
   em Testing com propósito explicitamente sintético. HTTP loopback só é permitido nesse ambiente;
   fora dele exigir HTTPS e hosts/origins configurados.
4. Migração é comando explícito `--migrate`, separado do startup. Identidade, banco/host e usuário
   são verificados; usuário de migração difere do usuário da aplicação. SQL embarcado tem controle
   de checksum e lock transacional para execução concorrente/repetida. Aplicação recebe DML nas
   tabelas próprias, sem DDL ou acesso ao controle de migrações. Não conceder credencial à Api.Agente.
5. Idempotência é garantida por constraint `(principal_id,type,idempotency_key_digest)`, e não por
   memória local. Registro e auditoria de aceitação são atômicos. Replay preserva operação original;
   payload divergente gera conflito. Preconditions usam ETag forte e diferenciam ausência (428)
   de revisão divergente (412); os casos de uso futuros devem verificar replay antes da revisão.
6. Intenção de consumo é confirmada antes da rede. Cada retry produz outra linha; métricas e custos
   desconhecidos permanecem nulos. Preço é opcional e exige referência, versão e moeda; a fórmula
   de estimativa é tokens de embeddings × preço por milhão / 1.000.000, em decimal. Falha depois
   da chamada e antes do registro final deixa intenção pending/unknown, sem repetir a chamada
   automaticamente. Não foi criada tabela de preços presumida ou consulta financeira pública.
7. Auditoria de efeito pertence à mesma Unit of Work de sua mutação. Auditoria de transporte é
   separada e registra pedidos permitidos/negados, erros e cancelamentos; não substitui auditoria
   transacional de domínio. Leitura interna de auditoria exige `knowledge.audit` central e filtro
   explícito de principal, com limite finito. Não existe endpoint público novo de relatório.
8. MCP stateless é protegido por `knowledge.read`; anuncia lista vazia de tools nesta fase, com
   handler explícito de listagem, sem simular conhecimento fiscal. Não registrar tools de escrita
   ou consulta REST funcional. Metadados de recurso protegido são públicos conforme OAuth;
   desafio Bearer indica a URL desses metadados. Tools fiscais ficam em suas histórias próprias.

## Consequências e revisão

Identidades, configurações e políticas de testes não aprovam produção. Comparação de perfis,
corpus, retenção real, backup e capacidade permanecem nos gates/tarefas posteriores.
As concessões SQL devem ser estendidas explicitamente quando novas tabelas de histórias forem
criadas. A raiz de composição registrará os adaptadores PDF/OpenAI quando as tarefas respectivas
existirem; não inserir adaptadores vazios para antecipá-las.

Revisar estas decisões se houver mudança do contrato de claims do emissor, necessidade de
consolidação externa de consumo, novo armazenamento ou mudança do transporte MCP.

## Fontes consultadas

- [ASP.NET Core — JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [ASP.NET Core — request timeouts](https://learn.microsoft.com/en-us/aspnet/core/performance/timeouts?view=aspnetcore-10.0)
- [Npgsql — transações e data source](https://www.npgsql.org/doc/basic-usage.html)
- [MCP C# — transporte](https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html), complementado pelos XMLs do pacote 2.3.0 restaurado.
## Revisão de conformidade — 2026-11-05

- ConsumptionPrice e ConsumptionMeasurement são objetos de valor do domínio. Validação de
  métricas/preço e cálculo decimal não pertencem mais ao repositório; o decorator transforma
  a resposta do provider em medição de domínio e o repositório apenas persiste os campos.
- currency registra exclusivamente a moeda informada pelo provedor. estimated_currency é
  independente e vem da tabela da estimativa. Valores em moedas diferentes são preservados,
  sem conversão; moeda omitida continua nula. Alteração interna aditiva, sem mudar REST/MCP v1.
- 0005_foundation_review.sql acrescenta a coluna e a constraint finished_at >= created_at;
  migrações anteriores/checksums são preservados. Nenhum valor histórico de moeda é inferido
  ou reescrito; registros anteriores mantêm a semântica histórica e não recebem moeda nova
  por suposição. Novos registros têm a separação explícita. Não houve operação com dados reais.
- AuthenticatedActor é utilizado pela validação JWT, autorização e auditoria de transporte.
  Somente uma identidade autenticada com sub fornece principal/organização; claims soltos
  em identidade não autenticada não identificam o ator auditado, que permanece anonymous.
