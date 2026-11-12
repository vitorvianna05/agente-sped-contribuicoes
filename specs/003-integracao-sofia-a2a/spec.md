# Feature 003 — Api.Agente especializada da SOFIA/INFIS

Data: 2026-11-10. Estado: especificada; A2A ainda não implementado.
Base: constitution §1, §2.11 e §3; confirmação expressa do usuário: orquestradora SOFIA/INFIS.
A documentação não altera Api.RAG, seus contratos, testes ou Feature 001.

## Escopo e responsabilidades

Usuário → SOFIA recebe solicitação/documentos e decide se deve acionar a especialidade →
Api.Agente executa análise SPED → SOFIA agrega resultados e responde ao usuário.
SOFIA possui interface/conversa, roteamento, registry de agentes e síntese multiagente.
Api.Agente possui parser/validadores, recuperação MCP e parecer especializado com evidências;
não decide quais outros agentes acionar nem assume a conversa/identidade autenticada do usuário.
A chamada à RAG continua sendo dependência técnica via MCP, não roteamento para outro agente.

Perfil acordado do projeto: A2A v0.3.x / JSON-RPC 2.0; contrato de negócio SOFIA v1.0.
Não confundir versaoContrato, versão do serviço e versão do protocolo. Referências:
[contrato](contracts/sofia-v1.md), [plano](plan.md), [tasks](tasks.md).

## Requisitos

| ID | Requisito verificável |
| --- | --- |
| FR-SO-001 | SOFIA centraliza recepção/roteamento/agregação; chama só agentes autorizados e necessários. Api.Agente responde somente por sua especialidade. |
| FR-SO-002 | Publicar Agent Card em /.well-known/agent-card.json, com identidade/configuração, skill analisar_documentos, endpoint url e capacidades efetivamente disponíveis. SOFIA usa url anunciado e validado, não o endereço do card como RPC. |
| FR-SO-003 | Receber message/send com SolicitacaoAnaliseSofia serializada em JSON UTF-8 em TextPart; validar versão/campos/limites e texto não confiável. Não usar POST REST de análise como substituto do A2A acordado. |
| FR-SO-004 | Preservar idExecucao/idSolicitacao e rastrear todos os idDocumento; agente.idAgente deve coincidir com registry/card. Criar IDs locais separados, sem sobrescrever os IDs de negócio recebidos. |
| FR-SO-005 | Reutilizar casos de uso por portas; adaptar documentos[].texto ao leitor SPED sem HTTP interno, perda de caracteres ou normalização de CRLF/campos. Não inferir bytes/hash original a partir de texto extraído. |
| FR-SO-006 | Retornar ResultadoAnaliseSofia em TextPart de Artifact da Task; distinguir estado protocolar e status concluido/parcial/erro do negócio. Não aceitar saída arbitrária do LLM como envelope SOFIA. |
| FR-SO-007 | Autenticar/autorizar SOFIA por transporte; usuario.nome e contexto não concedem permissão. Segregar contexto autorizado de usuário/organização quando disponível por mecanismo confiável pactuado. TLS e credenciais de serviço separados por destino. |
| FR-SO-008 | Repetição segura por principal autorizado + idAgente + idSolicitacao e digest do pedido; mesmo ID/payload retorna mesma operação, payload divergente conflita. Operação durável, prazos, cancelamento, tasks/get e tratamento de estados terminais. |
| FR-SO-009 | Retornar falha técnica/sem evidência/parcial/layout não suportado sem conformidade indevida; documentos não processados e verificações pendentes devem ser explícitos. SOFIA preserva respostas dos demais agentes. |
| FR-SO-010 | Logs mínimos sem usuario.nome, pergunta/textos/tokens; auditar IDs/etapas/custos com proprietários distintos. Política aprovada antes de persistir texto/Task/resultado real. |
| FR-SO-011 | Borda HTTP (ADR 0002): chamadas servidor-servidor da SOFIA, sem Origin, dependem só de rede, HTTPS efetivo, host permitido e JWT. Navegador só recebe permissão CORS se sua origem estiver em `Cors:AllowedOrigins` (política `SofiaBrowser`, vazia por padrão); origem desconhecida não recebe. TLS pode terminar em proxy listado em `Transport:*`; HTTP direto continua negado. |

## Aceite

- SC-SO-001: cliente SOFIA simulado descobre o card, envia A2A e recupera Artifact com schema,
  IDs e identidade corretos. Também acompanha operação pendente por tasks/get.
- SC-SO-002: dois documentos chegam integrais; IDs/linhas/CRLF e campos vazios não mudam.
  Alteração pela extração e texto não representável são detectados, nunca corrigidos em silêncio.
- SC-SO-003: replay não duplica análise/chamada faturável confirmada; outro payload, principal,
  task ou documento não autoriza reutilização/acesso indevido. Cancelamento/timeout não prometem
  desfazer chamada já confirmada; tentativas ambíguas ficam explícitas.
- SC-SO-004: parcial/unsupported/falha MCP/LLM/contrato são classificados sem afirmar conformidade.
  Outro agente simulado continua retornando seu resultado quando este falha.
- SC-SO-005: token errado/ausente, documento com instruções e URL de card arbitrária não burlam
  autorização, destino ou versão; logs não contêm dados pessoais/fiscais.
- SC-SO-006: preflight da origem pactuada recebe Allow-Origin/métodos/cabeçalhos; origem
  desconhecida não; chamada sem Origin e sem token recebe 401, sem permissão 403; cabeçalho
  encaminhado de remetente fora da lista não converte HTTP em HTTPS.

## Pendências antes de ativar

Confirmar com o responsável da SOFIA: idAgente/nome/URL e rede, issuer/audience/scopes e
credencial de serviço, mecanismo de contexto autorizado/delegação, limites/SLA/retentativas,
retenção de Task/IDs/resultados, preservação de texto SPED na extração e representação
compatível das referências RAG no contrato v1.0 (campos opcionais somente por acordo).
Não incluir campo obrigatório proprietário ou usar nome do usuário como credencial.

O serviço atual oferece leitura REST, análise REST parcial com parecer condicionado e diagnóstico
MCP; não possui Agent Card, servidor A2A nem análise fiscal integral. AG013 foi concluída contra
a Api.RAG real (2026-11-11); AG001/005/006/011/012/014 continuam com suas pendências. A borda
HTTP (CORS opcional e TLS em proxy confiável) está implementada (AA011).
Esta feature prepara a exposição A2A; testes sintéticos podem usar portas dublês, mas não
habilitam capacidade fiscal ou exposição real. Memória conversacional continua fora do escopo.
