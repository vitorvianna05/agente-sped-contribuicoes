# Executar Api.Agente e integração MCP inicial

Data: 2026-11-10. Exige SDK do global.json e Docker para a suíte completa (PostgreSQL descartável).
Nenhum teste exige chave/chamada OpenAI real. Há fluxo de análise estrutural/parecer parametrizado;
validação fiscal integral, política real e aceite de produção continuam pendentes.

## Testes isolados da nova API

```powershell
dotnet restore tests/Api.Agente.Tests/Api.Agente.Tests.csproj --locked-mode
dotnet build tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore
dotnet test --project tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/agente-mcp
dotnet format src/Api.Agente/Api.Agente.csproj --verify-no-changes --no-restore
dotnet format tests/Api.Agente.Tests/Api.Agente.Tests.csproj --verify-no-changes --no-restore
```

Esses comandos não recompilam a RAG nem requerem seu banco. Testes criam banco próprio descartável;
o servidor MCP sintético roda
em outro processo e usa contrato 1.0 congelado. Integração com a versão final da RAG em
alteração por outro LLM requer nova verificação explícita quando ela estiver estável.

## Development local com a Api.RAG

Iniciar a RAG conforme sua configuração própria, após o outro LLM concluir seus ajustes.
O destino inicial da Api.Agente é https://localhost:60994/mcp; alterar KnowledgeMcp:Endpoint
se o endereço real for diferente. Nenhum arquivo da RAG precisa ser alterado por este guia.

```powershell
dotnet run --project src/Api.Agente --no-launch-profile -- --environment Development --urls https://localhost:60996
```

O perfil Api.Agente em launchSettings também usa Development e https://localhost:60996.
A configuração Development versionada não contém segredos: permite identidade local somente
loopback, exige HTTPS e aponta para o MCP loopback sem bearer. Ela pressupõe que a RAG também
aceite seu modo local; se exigir JWT, configurar bearer de serviço externo e desativar
KnowledgeMcp:AllowUnauthenticatedLoopback. Api.Agente possui UserSecretsId próprio; usar
Gerenciar Segredos do Usuário nesse projeto ou variáveis de ambiente, nunca editar JSON com
chaves/senhas. Certificados TLS devem ser válidos/confiáveis; não há bypass no cliente MCP.

Abrir https://localhost:60996/health/live e
https://localhost:60996/admin/v1/knowledge/readiness. O segundo endpoint faz conexão real,
initialize/listTools e confere os schemas; não consulta conteúdo e não comprova perfil,
retenção ou qualidade fiscal. RAG indisponível/incompatível produz falha técnica explícita.

## Ambiente normal com JWT

O appsettings.Example.json é template incompleto e não é carregado automaticamente.
Fornecer Authentication__Authority, Audience, AllowedHosts e DiagnosticPrincipals;
Limits__MaxRequestBytes e TimeoutSeconds; KnowledgeMcp__Endpoint, BearerToken,
TimeoutSeconds, MaxResponseBytes e MaxQueryCharacters. Usar HTTPS; manter flags de dispensa
false. Bearer é credencial de serviço com knowledge.read na RAG, não o JWT de usuário da
Api.Agente. O chamador do diagnóstico precisa analysis.diagnose e atribuição administrativa.
Não enviar credenciais no chat, URLs, argumentos de tools ou logs.

Llm:Model é externo, com padrão gpt-4.1-mini confirmado pelo usuário; OpenAiParecerLlm já
está implementado com Structured Outputs/prompt v1.
Configurar Llm:ApiKey localmente, timeout, MaxAttempts, RetryDelayMilliseconds,
MaxOutputTokens, MaxContextCharacters e MaxOutputCharacters. O SDK não faz retries ocultos.
Não há avaliação oficial do modelo/corpus nem preços presumidos.

## Leitura do arquivo SPED

POST https://localhost:60996/api/v1/sped/leitura com analysis.execute (ou modo local
loopback autorizado). Enviar bytes do arquivo diretamente no corpo com Content-Type:
application/octet-stream ou text/plain; charset=iso-8859-1. Não enviar JSON, caminho local
ou multipart. [Contrato e exemplo curl](contracts/parser-sped.md).

Leitura sem ocorrências retorna readingStatus=completed, status=partial e
fiscalValidationStatus=not_performed. COD_VER é declarado pelo arquivo; não é comprovação
de compatibilidade. Ocorrências informam linha/código, e limites/codificações incompatíveis
falham explicitamente sem truncamento anunciado como sucesso. Resposta no-store e nenhum
arquivo é salvo ou enviado à RAG/OpenAI.

Limites positivos novos obrigatórios: SpedParser:MaxLineBytes, MaxRecords,
MaxFieldsPerRecord (inclui REG) e MaxIssues. Exemplos locais: 131072 bytes por linha,
100000 registros, 512 campos e 100 ocorrências; são limites técnicos ajustáveis, não fiscais.
Limits:MaxRequestBytes também limita o arquivo; TimeoutSeconds limita a requisição.
Testes e evidências: [parser-sped.md](validation/parser-sped.md).

## Consumo pela SOFIA/INFIS

A SOFIA receberá a requisição inicial e chamará este serviço como especialista via A2A;
contrato e tarefas estão na [Feature 003](../003-integracao-sofia-a2a/spec.md).
Agent Card/RPC/Task/Artifact ainda não foram implementados. Os endpoints REST de leitura e
diagnóstico deste guia são técnicos e não devem ser configurados como RPC A2A na SOFIA.
O modo Development sem JWT é loopback; não autoriza chamadas de rede/contêineres externos.

## Habilitar o fluxo de análise estrutural/parecer

POST /api/v1/analises usa os mesmos bytes/Content-Type da leitura, com analysis.execute.
[Contrato atual](contracts/analysis-rest.md). Antes de iniciar, é necessário configurar:

- AgentData:ConnectionString: banco próprio com usuário de aplicação, sem acesso ao banco RAG.
- AgentData:MigrationConnectionString: mesmo banco/host/porta, outro papel com permissão de
  criar schema/tabelas e conceder privilégios ao papel de aplicação. Não fornecer ao host HTTP
  depois da manutenção; retirar a variável/segredo administrativo de seu contexto de execução.
- AgentData:PolicyVersion, ApprovalReference, AuditDays, ConsumptionDays: valores aprovados,
  sem defaults. AuditDays deve cobrir ConsumptionDays para manter dono/correlação do consumo.
- AgentData:TimeoutSeconds e RetentionBatchSize positivos (limites técnicos, exemplos 10/500).
- LayoutPolicy:Profiles: COD_VER, From/Through ISO, DocumentType, KnowledgeLayoutVersion,
  SourceReference, ApprovalReference. Intervalos inclusivos em 2020–2026, sem sobreposição
  por código. Nenhum perfil real foi cadastrado: o tipo documental da RAG ainda não foi definido.
- Llm:ApiKey/configuração, dependência MCP estável e conhecimento elegível com contexto
  exatamente igual ao perfil aprovado. Não usar valores synthetic-* dos testes em operação real.

Com as conexões configuradas externamente, manutenção explícita (sem abrir o servidor HTTP):

```powershell
dotnet run --project src/Api.Agente --no-launch-profile -- --migrate-agent --environment Development
dotnet run --project src/Api.Agente --no-launch-profile -- --purge-agent --environment Development
```

Migração idempotente confere checksum e usa transação/lock. O papel HTTP recebe somente
USAGE/SELECT/INSERT/UPDATE no schema/tabelas da Api.Agente. Expurgo só remove registros já
vencidos, em lote, filhos antes dos pais; exige política preenchida e papel administrativo.
Cada invocação processa no máximo RetentionBatchSize tentativas e execuções. Não foi criado
agendamento: operação deve executá-lo conforme política real, com monitoramento de pendências.

Sem política/conexão, análise retorna 503/AUDIT_NOT_CONFIGURED antes de MCP/IA. Com política
mas sem perfil, retorna unsupported antes de consulta. Leitura simples permanece disponível.
Cobertura atual de regras é estrutural; mesmo com rascunho válido o retorno é partial e
humanReviewRequired=true, com todas as verificações fiscais não realizadas. Health/diagnóstico
continuam analysisAvailable=false: não são readiness de aceite fiscal/operação real.

GET /api/v1/analises/{executionId}/consumo exige mesmo subject/organization_id da execução.
Retorna tentativas/modelo/tokens conhecidos (inclusive em cache), duração e lacunas; o custo
estimado usa `Llm:Pricing` (ADR 0004) e fica nulo sem tabela ou sem uso informado. Não há dupla
contagem da RAG.
Não se guardam arquivo, prompt, trechos ou parecer no PostgreSQL.

Última evidência: [interoperability.md](validation/interoperability.md). As respostas do usuário
sobre períodos e política estão em [supported-layouts.md](validation/supported-layouts.md) e
[data-policy.md](validation/data-policy.md). Operação com a RAG final aguarda sua estabilização.

## Swagger da Api.Agente

Iniciar pelo perfil **Api.Agente** (Visual Studio) ou pelo comando Development deste guia.
O perfil já abre https://localhost:60996/swagger; o documento fica em
https://localhost:60996/openapi/v1.json. Interface e documento são exclusivos de Development
e respeitam HTTPS/AllowedHosts. Não é necessário iniciar a RAG para abrir a interface ou
testar somente leitura estrutural.

1. Expandir **POST /api/v1/sped/leitura** e clicar em **Try it out**.
2. Selecionar **application/octet-stream** e escolher o arquivo SPED original em Latin-1/CRLF.
3. Clicar em **Execute**. Arquivo é enviado diretamente no corpo, com os bytes preservados.
4. Para testar o fluxo de parecer, usar **POST /api/v1/analises** com os mesmos passos, após
   configurar a política/conexão, o perfil de leiaute, o MCP e o gerador descritos acima.
5. Usar executionId retornado em **GET /api/v1/analises/{id}/consumo**. O diagnóstico MCP fica
   em **GET /admin/v1/knowledge/readiness**.

Quando JWT estiver ativo, **Authorize** recebe o token da Api.Agente (somente o token, sem
prefixo Bearer). Leitura/análise/consumo exigem analysis.execute; diagnóstico exige
analysis.diagnose e atribuição administrativa. No modo Development loopback sem JWT já
autorizado, o botão não é necessário. Não informar chave OpenAI, senha PostgreSQL ou token
da RAG no Swagger; eles continuam na configuração externa do serviço.

O seletor envia arquivo bruto; não colar SPED em JSON/multipart. Respostas partial/failed/
unsupported em HTTP 200 devem ser interpretadas pelo campo status. Swagger não remove as
pendências fiscais ou os gates de dados. Evidência de AG015: [swagger.md](validation/swagger.md).
