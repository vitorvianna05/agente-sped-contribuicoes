# Perfil de integração SOFIA — contrato de negócio 1.0

Estado: contrato documentado; nenhum handler A2A ou DTO SOFIA executável foi criado.
Referência lida: skills locais sofia-a2a-contrato/references/contrato-sofia-v1.md e
exemplos-a2a-v03.md. Perfil de transporte: A2A v0.3.x, JSON-RPC 2.0.
[Especificação oficial fixada](https://a2a-protocol.org/v0.3.0/specification/).

## Mensagem de entrada

POST na URL validada de AgentCard.url; método message/send. params.message.role=user,
messageId, parts com kind=text e text contendo JSON UTF-8 de SolicitacaoAnaliseSofia.
O campo role=user do protocolo não é autenticação do usuário. Card em
/.well-known/agent-card.json; identidade e endereços efetivos são configuração pactuada.

| Campo de negócio | Exigência |
| --- | --- |
| versaoContrato | String 1.0. |
| idExecucao | UUID da execução SOFIA, preservado na resposta. |
| idSolicitacao | UUID por chamada ao agente; mesma chamada/retry preserva o ID. |
| usuario.nome | Obrigatório/não vazio; contexto de negócio, sem poder de autorização. |
| pergunta | Obrigatória/não vazia; não altera políticas, versão ou validações obrigatórias. |
| documentos | Array não vazio; cada item exige idDocumento, nome e texto não vazios; tipo MIME opcional. IDs sem ambiguidade. |
| contexto | Opcional; idioma/instrucoes não são prompt de controle nem identidade confiável. |

Não trocar nomes JSON por AnalysisRequest/AnalysisEnvelope do REST. Os contratos têm versões
independentes e serão mapeados na aplicação. Não adicionar binário, senha/token, caminho
local ou URL de download como substituto de documentos[].texto. Limites finitos para corpo,
documentos, caracteres, linhas, registros, resultados e duração; excesso falha sem truncar.

## Resultado e correlação

ResultadoAnaliseSofia em JSON UTF-8 em TextPart de Artifact de Task completed; extrair
artifacts, não presumir que status.message contém a saída. Task/context/message e RPC IDs
são do protocolo, diferentes dos IDs de negócio e dos executionId/correlationId locais.

| Campo | Exigência |
| --- | --- |
| versaoContrato | 1.0. |
| idExecucao / idSolicitacao | Exatamente os IDs da chamada autorizada. |
| agente | idAgente/nome obrigatórios, configurados; idAgente coincide com registry/card. |
| status | concluido, parcial ou erro. |
| analise | Texto obrigatório, inclusive para explicar impossibilidade ou ausência de evidência. |
| achados | Array obrigatório, pode ser vazio; idAchado/descricao/idDocumentos/evidencias conforme referência. idDocumentos só pode apontar para entrada. |
| achados[].severidade | Opcional: baixa, media, alta, critica; não inferir gravidade fiscal sem regra aprovada. |
| observacoes | Opcional; declarar limitações, documentos não processados e verificações pendentes. |
| erro | Opcional/nulo ou codigo/mensagem não sensíveis. |

Referências RAG continuam validadas internamente e localizáveis. A forma de expor seus IDs e
metadados estruturados sem perder rastreio precisa ser pactuada com SOFIA: v1 aceita campos
novos opcionais compatíveis, nunca nova obrigatoriedade unilateral. Não interpretar strings
arbitrárias em evidencias como citações verificadas. Contrato recebido não autoriza inventar
fonte, achado ou documento. Exemplos em ../examples são sintéticos, não saídas fiscais prontas.

| Estado interno | Resultado SOFIA planejado |
| --- | --- |
| completed com todas as etapas obrigatórias validadas | concluido, sem equivaler automaticamente a conformidade tributária. |
| partial / leitura sem validação fiscal | parcial + limitações explícitas. |
| unsupported | parcial + indicação de leiaute não suportado e ausência de conclusão; quando impede toda análise, erro com motivo. Não apresentar como ausência de inconsistências. |
| failed | erro com código/mensagem seguros. |

Task completed significa entrega de resultado de negócio, que ainda pode ser parcial/erro.
Falha protocolar/conexão não é ResultadoAnaliseSofia válido. Falha técnica impeditiva pode
terminar Task failed; cancelamento/timeout são explicitados e não produzem falso completed.
Negar solicitação ilegível/IDs ausentes sem inventar IDs de negócio. SOFIA preserva respostas
de outros agentes e agrega separadamente da análise original da Api.Agente.

## Segurança e dados

Autenticação/autorizações ficam no transporte; usuario.nome não substitui sub/tenant confiável.
Aceitar apenas SOFIA e contextos autorizados; checar destinatário, assinatura, expiração e
escopos. Cada hop tem credencial/audience própria. TLS é obrigatório fora de dev isolado
explicitamente autorizado; flags loopback atuais não cobrem rede Docker/host remoto.
Não persistir/logar pergunta, nome, documento ou token por convenção; retenção/consentimento
são condições externas anteriores a dados reais. RAG recebe só a consulta mínima via MCP.
