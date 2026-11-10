# Contrato MCP funcional v1

Endpoint `/mcp` na Api.RAG; Streamable HTTP stateless do SDK oficial conforme pesquisa datada
em [research.md](../research.md). Revisão de protocolo e contrato da aplicação são distintos:
protocolVersion pesquisada 2026-07-28; `contractVersion` de aplicação `1.0`. Cliente e servidor
devem fixar versões compatíveis e validar descoberta/listagem/chamada real antes da integração.
Metadados, cabeçalhos, cancelamento e erros de protocolo são responsabilidade do SDK/revisão
selecionada; não copiar handshake/sessão de exemplos legados.

## Segurança e responsabilidade

Bearer com recurso/audience da Api.RAG, HTTPS e validações documentadas no contrato REST;
metadados de recurso protegido conforme protocolo. Cada chamada exige `knowledge.read`.
A mesma permissão autoriza resolução histórica de conteúdo retido; não concede administração.
Validar Origin quando presente e hosts permitidos. Identidade/correlação não são argumentos
confiados ao modelo. Inputs são dados, não instruções para mudar política. Não há tools de escrita.
Api.Agente utiliza MCP e valida schemas; sem fallback REST e sem acesso ao banco da Api.RAG.

## Tools

Schemas explícitos em [mcp-tools.schema.json](mcp-tools.schema.json), com definições
`SearchInput`, `ResolveInput` e `Result`. Cada tool anuncia inputSchema e outputSchema
correspondentes; o arquivo é catálogo de schemas, não formato de mensagem JSON-RPC alternativo.

| Tool | Responsabilidade | Entrada | Saída |
| --- | --- | --- | --- |
| `BuscarConhecimento` | Recuperar trechos de fontes publicadas, aplicabilidade oficial e geração/perfil elegíveis. | SearchInput: consulta, tipoDocumento, versaoLayout. | Result com evidências, referências e limitações. |
| `ResolverReferencia` | Recuperar exatamente o trecho citado, inclusive histórico retido autorizado. | ResolveInput: referenceId. | Result com evidência exata e estado active/superseded/withdrawn, ou indisponibilidade tipada. |

Não aceitar topK, limiar, perfil arbitrário, identidade, bypass de versão ou URL de download
em argumentos. O servidor usa o perfil configurado: `Retrieval:AdoptedProfileId` ou, se vazio, o
único perfil de `Processing:Profiles`, compatível com provedor/modelo/dimensão de `Embeddings`
([ADR 0009](../adrs/0009-simplificacao-ingestao-busca.md)); `TopK` ausente = 5 e `Threshold`
ausente = sem corte. Perfil ausente, ambíguo ou incompatível é falha de configuração
(`PROFILE_INCOMPATIBLE`), não consulta concluída sem evidência. Strings vazias são inválidas mesmo com formato JSON válido.
Tamanhos máximos e timeout devem vir de configuração finita P04, aplicada além do schema.

## Semântica de resultado

`Result` requer versão, correlação do sistema, status, evidences, limitations e error.
Uma evidência contém texto, referência, identidades de fonte/versão/geração/trecho, versão
oficial do manual e aplicabilidade de layout, origem e localização disponível/ausente.
Score é técnico, opcional; não é confiança fiscal. Campos operacionais nunca são inventados
por modelo; não existe gerador de parecer nesta feature.

- `evidence_found`: há trechos retornados. Isso não significa que toda a pergunta foi respondida
  ou que a análise está conforme. Incluir limitação de cobertura; suficiência fiscal não é aferida.
- `insufficient_evidence`: busca concluída sem evidência elegível suficiente conforme política
  avaliada, com limitação explícita. Pode incluir evidências parciais; não fabricar referência.
- `invalid_context`: tipo/versão/contexto de negócio insuficiente; não presumir versão. Formato
  inválido continua erro de protocolo/schema. O cliente pode corrigir o contexto, não fazer retry cego.
- `reference_unavailable`: referência inexistente (ou com tombstone de expurgo anterior à
  ADR 0009; a API não expurga mais); evidences vazio, limitação e código
  REFERENCE_NOT_FOUND/REFERENCE_PURGED. Nenhum texto substituto.
- `technical_failure`: dependência, timeout, perfil ausente ou erro de execução; evidences vazio,
  error não nulo, correlação e `isError: true` no envelope da tool. Não disfarçar como busca vazia.

Sem falha técnica, `error` é null. Ausência de evidência é resultado normal com `isError: false`.
invalid_context também é resultado de domínio normal para contexto incompleto; cliente valida
status. Erros JSON-RPC (método/versão/forma inválida) ficam no envelope do protocolo e não em
um Result de sucesso. Transportes podem negar 401/403/429 antes da execução. O resultado
structuredContent deve obedecer ao schema e deve ter cópia textual JSON equivalente para clientes no padrão ConhecimentoMcpClient que leem TextContentBlock.

## Invariantes semânticas além do schema

1. Busca aplica filtro relacional de tipo, layout/aplicabilidade, publicação, geração e perfil.
   Identificadores de manual e layout não são intercambiáveis. Cada evidência comprova a relação.
2. Nunca misturar gerações da mesma versão em um resultado. Troca/retirada concorrentes são
   vistas por snapshot consistente. Contexto e referências devem ser auditáveis pelo servidor.
3. `evidence_found` exige evidência não vazia; `reference_unavailable`, `invalid_context` e
   `technical_failure` exigem zero evidências. `insufficient_evidence` pode conter evidência parcial.
4. Resolução retorna no máximo um trecho; não inclui busca por similaridade ou atualização
   automática de referência. Estado withdrawn (versão substituída por outra versão da mesma
   fonte) ou superseded (geração substituída por reenvio) não impede a resolução.
5. location unavailable exige reason e nenhum número de página inventado; location available
   exige localizador observado. Rótulo impresso e página física ficam separados.
6. technical_failure exige error e correlação, demais status exigem error null. A descoberta de
   qualquer violação no cliente rejeita o resultado como falha de contrato, nunca como evidência.
7. Toda resposta inclui limitations, ainda que apenas para registrar que recuperação não
   comprova suficiência ou conformidade fiscal; nada de referências artificiais em respostas vazias.

## Limites, cancelamento e retries

Tools são de leitura e idempotentes quanto ao conhecimento, porém busca com embedding pode gerar
custo em cada tentativa. Timeout/retry são limitados por configuração; cliente respeita política
e orçamento, sem retry sobre entrada inválida ou permissão negada. Propagar cancelamento até
banco/provedor conforme SDK; registrar tentativa interrompida e consumo desconhecido quando
necessário. Nenhum retry contorna versão ou troca protocolo.

## Aceite

Cliente real deve descobrir/listar tools, validar schemas e executar ambas. Cobrir autorização,
sem resposta, versão incompatível, localização indisponível, falha externa, substituição por nova
versão, cancelamento e concorrência. Medir pelo cliente todas as consultas da amostra de SC-009/010,
incluindo embedding e resposta completa, no perfil de 300 PDFs e 5 consultas simultâneas.
≥95% devem concluir em até 5s; falhas/timeouts não são sucessos e não saem do relatório.
## Revisão de integração — 2026-11-05

Adota o padrão de classes Tools da Api.Conhecimento e cliente Mcp da Api.Atendimento
inspecionados em CorporativoAI: HttpClientTransport → McpClient.CreateAsync → ListToolsAsync →
CallToolAsync. Os nomes antigos fiscal_knowledge_search_v1/fiscal_reference_resolve_v1 eram
planejados, não implementados/publicados; são substituídos por BuscarConhecimento/ResolverReferencia.
Result continua contractVersion 1.0. A fundação implementada anuncia lista vazia e não foi
alterada por esta revisão documental; T041/T042 implementarão e validarão o contrato novo.

A assinatura fiscal estende a referência: consulta + tipoDocumento + versaoLayout obrigatórios.
O cliente da referência, que envia apenas consulta e espera texto livre, precisa ser adaptado.
O consumidor valida structuredContent ou a cópia JSON textual, confronta os dois se ambos
existirem e rejeita divergência. Conteúdo TextContentBlock não é aceito como evidência se não
obedecer ao schema e às invariantes. A futura Api.Agente usa esses trechos para o parecer
OpenAI após as validações determinísticas; geração não ocorre nesta tool ou na Api.RAG.

Exemplo de arguments (placeholders técnicos, não versão oficial aprovada):

```json
{"consulta":"Fundamento para a inconsistência encontrada","tipoDocumento":"<tipo-identificado>","versaoLayout":"<versao-identificada>"}
```

## Implementação dos ajustes 5–11

As duas tools estão registradas nos handlers do SDK 2.3.0 com schemas do catálogo resolvidos,
saída estruturada/textual validada e autorização por execução. Os trechos seguem as regras de
aplicabilidade, publicação, perfil e histórico efetivamente ativado. O consumidor de testes em
processo separado valida os schemas e confronta texto/structuredContent antes de aceitar saída.
A descrição anterior de lista vazia é histórica da fundação. Evidências em validation/rag-complete.md.

## Simplificação — 2026-11-10

Contrato das tools inalterado. Muda apenas o que é elegível: a versão entra na busca assim que
texto e vetores concluem (sem `approve`), a versão mais recente de uma fonte retira as anteriores,
e o perfil é o configurado, sem catálogo nem perfil `adopted`. Ver ADR 0009.
