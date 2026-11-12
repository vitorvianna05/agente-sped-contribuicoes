# Plano — SOFIA cliente A2A / Api.Agente servidor especializado

Data: 2026-11-10. Desenho, sem implementação; não modificar Api.RAG ou seu consumo MCP.

## Limites e fluxo

1. SOFIA recebe/autentica o usuário, controla sessão/organização e obtém documentos; seleciona
   a especialidade no registry. A Api.Agente não realiza roteamento multiagente.
2. SOFIA valida card, URL/configuração e perfil A2A v0.3.x JSON-RPC; envia message/send com
   JSON de negócio v1.0 em TextPart. Não há arquivos binários no contrato compartilhado.
3. Adaptador A2A da Api.Agente autentica o serviço, valida contrato/limites/contexto autorizado,
   correlaciona e registra operação idempotente. Recepção não significa análise concluída.
4. Caso de uso da aplicação transforma documentos[].texto para entrada textual do parser,
   identifica leiaute/validações suportadas e coordena validação → MCP → parecer validado.
   Não chama seus próprios endpoints REST e não expõe o banco da RAG.
5. Resultado validado é mapeado para ResultadoAnaliseSofia em Artifact; Task é acompanhada
   por tasks/get quando pendente. SOFIA agrega o resultado original e produz síntese separada.

## Organização alvo dentro do projeto existente

- A2a/Contracts: DTOs locais do contrato SOFIA v1.0, nomes JSON em português sem renomeios.
- A2a: adaptador JSON-RPC, Agent Card, mapeamento Message/Task/Artifact e autenticação.
- Analise: porta/caso de uso que recebe documentos textuais e devolve resultado especializado.
- Validacao: porta textual compartilhando regras lexicais com ISpedParser, sem acoplar
  domínio ao SDK A2A, converter UTF-8 JSON em Latin-1 com fallback ou perder terminadores.
- Infrastructure: Task store/leases/idempotência/auditoria próprios da Api.Agente, com política
  e credencial próprias; não usar tabelas/credenciais internas da Api.RAG.

Sem projeto extra por camada. Inspecionar SDKs .NET instalados e compatibilidade v0.3.x antes
de codificar; não presumir pacote/framework nem instalar versão recente sem validação.
Contrato e segurança antecedem endpoints; não implementar JSON-RPC ad hoc fingindo outro perfil.

## Texto SPED e proveniência

SOFIA deve preservar texto SPED: não OCR, resumir, aparar espaços, reordenar registros,
converter CRLF em LF ou remover campos vazios. Documento malformado continua malformado.
JSON usa UTF-8 e escapes de CRLF; isso não altera a regra Latin-1 do arquivo SPED original.
O adaptador trabalha com texto Unicode já decodificado e informa origem sofia_text.
Se reutilizar caminho de bytes, exigir representabilidade Latin-1 e conversão sem replacement;
preferir porta de texto com a mesma regra lexical. Texto não representável falha explicitamente.

Hash do texto transportado serve à idempotência/integridade do payload, não é SHA-256 do
arquivo original. Hash/encoding dos bytes originais e equivalência com arquivo recebido não
podem ser afirmados sem evidência; preservar desconhecidos nulos. Localização de achado é
linha no texto recebido e idDocumento, sem prometer a mesma linha do original se houve extração.

## Identidade, IDs, estados e custo

Credencial SOFIA→Agente tem audience/permissão da Api.Agente; credencial Agente→RAG é distinta.
Não reenviar automaticamente JWT do usuário/SOFIA à RAG. usuario.nome é dado de negócio,
contexto.instrucoes/pergunta/documentos são não confiáveis e não mudam tools/permissões.
Manter idExecucao/idSolicitacao externos, executionId/correlationId locais e A2A task.id,
contextId/messageId/RPC id separados. Os middleware REST atuais não preservam IDs SOFIA
por si só: a futura camada A2A deve mapeá-los sem mudar a regra de geração dos IDs locais.

O acesso a Task/resultado e a chave idempotente incluem principal autorizado/contexto aplicável.
Mesmo idSolicitacao com payload diferente conflita; scopes/expiração/delegação são revalidados.
Política de retenção cobre também metadados de Task/idempotência; não persistir dados reais
sem aprovação. Logs contêm IDs mínimos/etapas, não usuario.nome ou textos. Custos gerativos
são da Api.Agente; embeddings da RAG, correlacionados sem dupla contagem.

Timeout/cancelamento do transporte não apagam efeito já confirmado. Definir estado durável,
leases e recuperação antes de anunciar tarefas assíncronas. Plano inicial sem streaming/push;
capabilities do card devem corresponder à implementação, não à ambição do projeto.

## Entrega incremental

Adapter/contratos e testes sintéticos podem avançar antes de AG012 usando porta dublê que
retorna parcial explícito. Análise fiscal real depende de AG001/006/009–014 e gates de dados/
ambiente. Não registrar analisar_documentos como análise fiscal pronta enquanto indisponível;
acertar disponibilidade/capacidade com SOFIA. Não abrir bypass Development a outra máquina ou
contêiner: configuração loopback atual não autoriza integração externa.

## Borda HTTP (skill sofia-cors-rest-a2a; ADR 0002)

- CORS não habilita A2A: SOFIA backend → agente é HTTP comum (rede, DNS, TLS, JWT). Política
  `SofiaBrowser` só para navegador de origem pactuada, via `Cors:AllowedOrigins` por ambiente
  (vazia por padrão), aplicada aos controllers; health/Swagger fora. Sem credenciais de cookie.
- Ordem: cabeçalhos encaminhados (somente de `Transport:TrustedProxies/TrustedNetworks`) →
  guarda de transporte/limites/correlação → CORS → autenticação → autorização → endpoints.
- Contêiner: `ASPNETCORE_URLS=http://+:8080` atrás de proxy TLS, nome do serviço em
  `Authentication:AllowedHosts`, rede do proxy em `Transport:TrustedNetworks`. SOFIA usa o nome
  do serviço na rede interna. Publicação: skill `sofia-devops-free-cloud`.
- Endpoint A2A e Agent Card (AA005/AA007) herdam a mesma borda; CORS neles só se um navegador
  precisar. Testes de DNS/porta a partir do contêiner da SOFIA ficam em AA009.

A alteração do cliente/registry da SOFIA é trabalho do seu proprietário. Neste repositório
há apenas contrato de integração e servidor futuro da Api.Agente. Ver tasks.md e ADR 0001.
