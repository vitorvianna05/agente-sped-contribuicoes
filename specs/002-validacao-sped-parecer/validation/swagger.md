# Evidência — Swagger da Api.Agente (AG015)

Data: 2026-11-10. Solicitação: implementar Swagger para testes na Api.Agente.
Rastreio: FR-AG-001/008/009. Api.RAG/Feature 001 não foram alteradas neste incremento.

## Entrega

- Microsoft.AspNetCore.OpenApi 10.0.14 e SwaggerUI 10.3.1, versões centrais existentes,
  adicionados ao projeto/lockfiles da Api.Agente. Nenhum novo projeto ou provedor de dados.
- /swagger e /openapi/v1.json somente em Development, sob HTTPS/AllowedHosts existentes.
  O perfil Api.Agente já anuncia https://localhost:60996/swagger.
- Dois POSTs com schema string/binary e application/octet-stream como primeira opção;
  upload bruto do arquivo original Latin-1/CRLF, sem criar rota multipart ou alterar parser.
- Bearer por operação conforme esquema de autenticação realmente ativo; health sem exigência
  de JWT e modo local sem token fictício. Nenhum segredo/credencial embutido no documento.
- Endpoints/escopos/estados/erros e pré-requisitos da análise documentados; schema do resultado
  de análise gerado a partir de AnalysisResult. Swagger não desativa políticas ou aprova suporte fiscal.

## Verificação

129 testes aprovados, zero falhas e zero ignorados. Build sem avisos/erros; restore
--locked-mode aprovado e formatação dos dois projetos verificada.

Quatro cenários novos de SwaggerTests:

| Cenário | Evidência |
| --- | --- |
| Documento real / JWT | Rotas dos cinco endpoints, binário obrigatório nos dois uploads, ausência de multipart, requisitos Bearer e health anônimo; resultado JSON e pré-requisito de auditoria descritos. |
| UI e assets locais | HTML/JavaScript respondem 200; host não permitido recebe 403 para UI e documento. |
| Modo local e bytes | Sem Bearer no schema local; requisição com o media type anunciado preserva João/campos e retorna fiscalValidationStatus=not_performed, sem RAG/IA/banco. |
| Ambiente normal | Production retorna 404 para /swagger/index.html e /openapi/v1.json. |

Os testes usam Kestrel/porta efêmera, dados sintéticos e simulação HTTPS exclusiva do host de
teste; não relaxam o guard em produção. Foram verificados HTML/assets e contrato HTTP, sem
alegar inspeção visual de navegador. A suíte de regressão inclui os cenários anteriores de
MCP separado/PostgreSQL descartável; nenhuma chamada OpenAI real é necessária.

Comandos: restore/build/test/format do quickstart, usando
`--results-directory TestResults/agente-swagger-verified`. TRX local ignorado pelo Git:
`INFIS001125_INFISPC0064_2026-11-10_15_58_18.848002.trx`, 129/129 em 43,142 segundos.
As pendências de política, mapeamento do leiaute e integração real permanecem em tasks.md.

Referências técnicas consultadas: [Microsoft — customização de OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
e [Swagger — upload binário](https://swagger.io/docs/specification/v3_0/describing-request-body/file-upload/).
Como usar: [quickstart.md](../quickstart.md).
