# ADR 0005 — Ingestão textual antes da integração de embeddings

> **Nota (2026-11-10):** a ingestão textual permanece; reenvio sem Idempotency-Key, publicação automática após vetores e remoção de approve/reprocess estão na [ADR 0009](0009-simplificacao-ingestao-busca.md).

Data: 2026-11-05. Decisão: implementar os ajustes 1–4 solicitados, preservando o desenho
completo da feature. Embeddings reais, busca, aprovação e tools funcionais são incrementos seguintes.

## Decisão

Controllers recebem multipart e expõem somente acompanhamento administrativo. IngerirFonte
coordena política, validação, hash e porta de persistência; Ingestao contém os adaptadores
PdfPig e segmentadores. O domínio não depende de ASP.NET, PDF ou PostgreSQL. Rag/Tools
continuam reservados aos componentes previstos nas tarefas futuras, sem classes vazias.

O upload confirma em transação original bytea, metadados, aplicabilidade, perfil imutável,
operação, geração e auditoria. A fila é a própria tabela de gerações. Workers reivindicam
trabalho com SKIP LOCKED e lease persistente; não mantêm transação durante extração.
Confirmação exige o mesmo dono e lease vigente. Interrupção permite retomada por outro processo.

`preparationState` é um estado interno adicional: queued → processing → ready | failed.
Quando o texto está pronto, a geração completa permanece processing, a operação fica queued
com `resultCode=EMBEDDINGS_PENDING`, e completedAt/finishedAt permanecem nulos. Não há polling
do worker textual sobre gerações ready. O próximo incremento deverá consumir esse estágio
durável e concluir embeddings antes de marcar succeeded. Falha textual torna operação e
geração failed, sem nenhum trecho parcial. Publicação permanece unpublished e sem ponteiro ativo.

O perfil completo é persistido e comparado por hash. Mudança sob o mesmo UUID é recusada;
alterações de configuração exigem outro UUID. Um candidato não é adotado automaticamente.
O tokenizer `whitespace-v1` conta ocorrências lexicais separadas por espaços, exclusivamente
para o experimento de segmentação; não representa BPE, limite de contexto ou faturamento OpenAI.
Normalização `line-endings-v1` altera somente quebras de linha. Spans usam offsets UTF-16 no
texto extraído e normalizado de cada página, não coordenadas gráficas do PDF.

Estratégia structural respeita parágrafos observados e limita cada ocorrência com janelas;
token-window limita cada página. Overlap e tamanho vêm do perfil externo. Não se unem páginas,
não se deduplicam ocorrências iguais e não se inferem seções ou rótulos impressos. Cada trecho
ganha UUID/referência próprios e localização física observada.

## Alternativas e consequências

Não gerar vetores fictícios nem concluir uma operação que ainda depende de embeddings.
O fluxo síncrono da referência simplificaria o código, mas perderia a fila durável especificada.
Não introduzir broker ou projeto adicional: a transação PostgreSQL já garante o recebimento.

O worker respeita MaxConcurrentJobs por instância, timeout e lease externos (lease > timeout).
Revisão de princípios: MaxPreparationAttempts também é externo, positivo e obrigatório.
Interrupções ou falhas que impeçam confirmar o desfecho consomem tentativas persistidas;
ao esgotá-las, a retomada registra failed/PROCESSING_ATTEMPTS_EXHAUSTED com auditoria.
Falha técnica observada durante um job registra failed/TECHNICAL_FAILURE; timeout registra
OPERATION_TIMEOUT. Indisponibilidade do banco pode adiar a gravação até a recuperação,
mas não autoriza executar indefinidamente o mesmo job. Shutdown preserva o lease para retomada.
PdfPig tem parser síncrono: cancelamento é cooperativo entre páginas; não há encerramento
forçado de uma chamada de parsing em curso. Isolamento em processo é uma evolução se o ambiente
exigir limite rígido de CPU/memória para PDFs hostis. Toda página sem texto útil é recusada,
inclusive uma página em branco; política conservadora evita aceitar extração parcial/OCR.

Upload exige MIME application/pdf e assinatura, limite real de bytes, metadados estritos,
JWT e atribuição central. Limite HTTP soma 128 KiB de overhead ao arquivo; metadata tem
64 KiB e campos limitados. Original/reference retention precisa estar configurada antes do efeito.
URL é somente metadado HTTPS: nenhuma requisição externa é feita ao endereço informado.
Identificação declarada não equivale à aprovação da autoridade documental.

Reenvio com a mesma chave/payload retorna a operação original. Outra chave para a mesma
fonte/versão/perfil acompanha a geração canônica sem criar trechos. O DTO, Location e ETag
usam a operação canônica completa, inclusive operationId e createdAt. Uma chave adicional
continua registrada para idempotência/auditoria, sem misturar os horários do registro adicional
com os horários da execução original. Hash distinto gera conflito;
mudança de origem ou aplicabilidade também exige caso de uso próprio, sem sobrescrita implícita.
Perfis antigos continuam disponíveis ao worker a partir do snapshot persistido.

Rever esta decisão ao implementar T029/T032/T038–T041: completar o estágio embeddings,
manifesto integral, publicação e elegibilidade, mantendo compatibilidade de acompanhamento.
