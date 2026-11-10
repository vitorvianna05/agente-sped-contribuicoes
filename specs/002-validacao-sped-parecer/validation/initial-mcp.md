# Evidências — etapa 3, iniciação da Api.Agente e MCP

Data: 2026-11-10. Escopo: AG002/AG003/AG004/AG007/AG008. Código exclusivo de Api.Agente e
Api.Agente.Tests; solução recebe os dois projetos preservando itens existentes. Sem alterações
na Api.RAG, seus testes, configuração ou Feature 001 em trabalho concorrente por outro LLM.

## Rastreio do incremento

| Task / requisito | Entrega | Testes |
| --- | --- | --- |
| AG002; FR-AG-001/009 | Dois projetos net10.0, configuração externa/validada, UserSecretsId, lockfiles, portas locais distintas, estrutura funcional e provedores sintéticos somente nos testes. | Build/restore; ConfigurationTests; servidor MCP de fixture fora do processo. |
| AG003; FR-AG-001/008 | Metadata de entrada versionada/estrita, estados técnicos no domínio, envelope operacional separado do ParecerDraft. | AnalysisContractTests: campos ausentes, versão incompatível, identidade/layout arbitrários, chaves duplicadas, estados técnicos e rejeição de metadados do sistema no JSON do LLM. |
| AG004; FR-AG-001/009 | JWT/subject/audience/assinatura/expiração, policies, atribuição administrativa, HTTPS/hosts, corpo limitado, prazo/cancelamento, executionId/correlationId do sistema e logs mínimos. Exceção Development loopback própria. | AgentSecurityTests: anônimo, JWT inválido/subject ausente, scope inadequado/principal não atribuído, diagnóstico autorizado, tamanho, host e correlação não atribuída pelo chamador. LocalAuthenticationTests: IPv4/IPv6/mapped, não loopback e IP ausente. ConfigurationTests: bypass proibido em produção/URLs inseguras/limites inválidos. |
| AG007/AG008; FR-AG-003/004 | SDK Streamable HTTP explícito, descoberta/confronto de schema, busca e resolução com DTOs locais, bearer de serviço, timeout/cancelamento/limites de resposta. Sem REST/SQL/fallback. | KnowledgeClientTests: processo separado, busca/resolução, text-only compatível, JSON/structured divergentes, schema/tool ausentes, layout/referência incorretos, IsError inconsistente, erro remoto, resposta grande, HTTP 401/503, token de serviço, contexto antes de rede, timeout e cancelamento antes/durante transporte. |

O exemplo local usa https://localhost:60996 para Api.Agente e destino
https://localhost:60994/mcp para a RAG. Não contém token ou chave. O diagnóstico só confere
transporte/schema e declara analysisAvailable=false; não é resultado fiscal ou evidência.

## Execução e limites

O transporte foi testado com servidor MCP sintético em processo independente, contrato 1.0
congelado, Kestrel real e JWT de teste. Não há conexão ao banco ou à instância da RAG em
alteração, nem OpenAI/embeddings faturáveis. A compatibilidade com a versão final concorrente
da RAG deverá ser confirmada depois de estabilizada; esta evidência não conclui AG013.

Primeira execução falhou na fixture porque Configuration.Sources.Clear removia também o
provider necessário para UseUrls; corrigido com provider em memória vazio, sem herdar secrets.
Houve ainda um teste de conexão recusada cujo tempo no Windows podia atingir o deadline;
a indisponibilidade passou a ser comprovada por HTTP 503 controlado e validação/cancelamento
antes da rede continuam independentes. A janela sintética normal de 2s foi insuficiente em
uma execução concorrente: ajustada para 15s, mantendo teste específico de timeout em 1s e
limite HTTP de fixture 30s. Esses valores não constituem ensaio de capacidade ou mudança na RAG.

## Resultado final comprovado

- 42 testes executados, 42 aprovados, 0 falhas e 0 ignorados.
- Build isolado de Api.Agente + Api.Agente.Tests: 0 avisos e 0 erros.
- Restore --locked-mode dos novos projetos: aprovado.
- dotnet format --verify-no-changes dos dois projetos: exit 0.
- Perfil local Api.Agente iniciou em Development/HTTPS na porta 60996; GET /health/live
  respondeu 200 com status=alive e analysisAvailable=false; host de verificação encerrado.
  O certificado de desenvolvimento do ambiente não é confiável: somente a sondagem health
  usou SkipCertificateCheck, sem alterar validação TLS do cliente MCP ou confiança do sistema.
- TRX local não versionado: `TestResults/agente-mcp-verified/INFIS001125_INFISPC0064_2026-11-10_13_09_19.512401.trx`.

```powershell
dotnet restore tests/Api.Agente.Tests/Api.Agente.Tests.csproj --locked-mode
dotnet build tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore
dotnet test --project tests/Api.Agente.Tests/Api.Agente.Tests.csproj --no-restore --report-xunit-trx --results-directory TestResults/agente-mcp-verified
dotnet format src/Api.Agente/Api.Agente.csproj --verify-no-changes --no-restore
dotnet format tests/Api.Agente.Tests/Api.Agente.Tests.csproj --verify-no-changes --no-restore
```

Os projetos da RAG não foram compilados ou testados por estes comandos. Sua execução de
94 testes é histórica de outro incremento e não é somada como nova validação deste serviço.

## Fora do escopo / próximos passos

AG001, AG005/AG006 e AG009–AG014 continuam pendentes: fontes/layouts/regras oficiais e
política de dados, parser/validadores SPED, gerador OpenAI, confronto completo de citações,
consumo/auditoria persistidos da Api.Agente, endpoint de análises e aceite ponta a ponta/fiscal.
Apenas logging técnico foi implementado neste incremento; não é a persistência de AG011.

Não houve alteração de pacote central, banco real, implantação, commit ou push. A escolha
futura de gpt-4o-mini não é geração implementada nem avaliação aprovada. P01–P04 permanecem.
Guia: [quickstart](../quickstart.md); contratos: [foundation](../contracts/foundation.md);
decisão: [ADR 0001](../adrs/0001-fundacao-cliente-mcp.md).
