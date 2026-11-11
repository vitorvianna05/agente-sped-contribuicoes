# ADR 0011 — Relevância da busca: híbrida, seções por registro e consulta do Agente

Data: 2026-11-11. Status: aceito. Responsável: Vitor Vianna. Complementa a ADR 0009 (perfil
configurado na busca) e a ADR 0010 (perfil de desenvolvimento). Os parâmetros continuam **não
avaliados oficialmente** (T069/T077 pendentes); a medição abaixo usa um conjunto técnico de
desenvolvimento.

## Contexto

Com o Guia Prático EFD-Contribuições 1.35 ingerido (perfil token-window 400/50, só cosseno), a
busca devolvia sumário, cabeçalhos repetidos e páginas genéricas: hit@5 57% e MRR 0,332 em 15
consultas. A Api.Agente enviava códigos internos de regra (`EFD.0000.PERIOD:passed`), sem
vocabulário do guia, e não recebia nenhuma das páginas esperadas (33, 66–67, 416).

## Decisão

1. **Busca híbrida.** Migração `0010_lexical_search`: coluna gerada `rag.chunks.text_search`
   (`to_tsvector('portuguese', seção + texto)`) com índice GIN. `KnowledgeRetrieval` combina os
   50 melhores por cosseno e os 50 melhores por `ts_rank_cd` (termos da consulta unidos por OR)
   via Reciprocal Rank Fusion (k = 60). Elegibilidade (tipo, layout, publicação, geração ativa,
   perfil) é a mesma de antes. O `score` devolvido continua sendo a similaridade de cosseno;
   `Threshold`, quando configurado, continua sendo corte de cosseno; a ordem é a da fusão.
2. **Normalização `repeated-lines-v1`.** Remove linhas cujo texto (dígitos normalizados) aparece
   em ≥ 80% das páginas, com mínimo de 3 páginas (cabeçalho "Guia Prático … Atualização em …").
   Página que ficaria vazia mantém o texto original. `line-endings-v1` continua válido.
3. **Segmentação `record-sections`.** Cada título "Registro XXXX: …" no início de linha abre um
   trecho e define `location.section`, herdada pelas páginas seguintes até o próximo título.
   Entradas de sumário (pontilhado até o número da página, inclusive com título quebrado em até
   três linhas) não abrem seção; página com 3+ linhas de sumário é segmentada como token-window,
   sem seção, e encerra a herança. A seção é observada no texto, não inferida.
4. **Embedding com seção.** O texto enviado ao modelo é `seção + "\n" + texto`; o texto
   persistido e citado continua exatamente o extraído.
5. **Consulta da Api.Agente.** `KnowledgeQuery.Build` traduz as regras executadas em títulos do
   guia (Características do Arquivo Digital; Registro 0000 e DT_INI/DT_FIN/COD_VER; Registro
   9999/QTD_LIN), com prefixo de leiaute e período. Regras desconhecidas não entram; nenhum
   conteúdo do arquivo do contribuinte vai para a busca.
6. **Perfil de desenvolvimento.** Por serem imutáveis por UUID, as mudanças de segmentação
   geraram novos perfis; o vigente é `d0c0a1e0-0000-4000-8000-000000000005`
   (`repeated-lines-v1`, `record-sections`, 400/50, text-embedding-3-small/1536, TopK 5, sem
   limiar). Gerações de perfis anteriores permanecem no banco local, fora da busca.

## Medição (conjunto técnico, não aprovado)

`specs/001-base-fiscal-versionada/evaluation/busca-guia-1.35.json`: 15 consultas montadas a
partir dos títulos do próprio guia, com páginas esperadas; medido por
`scripts/local/avaliar-busca.ps1` contra a Api.RAG local.

| Etapa | hit@5 | MRR |
| --- | --- | --- |
| Baseline (token-window, só cosseno) | 57% | 0,332 |
| Só busca híbrida | 86% | 0,544 |
| Híbrida + perfil v5 | 93% (14/15) | 0,844 |

A única falha é `AGENTE-V1`, a consulta antiga da Api.Agente, mantida como referência negativa;
a consulta nova (`AGENTE`) acerta na posição 1. Na geração v5 (896 trechos, 796 com seção) a
primeira página com seção é a 66 — nenhuma página de sumário herda seção. Ponta a ponta, a
análise de um arquivo sintético válido recebeu p.67 e p.68 (Registro 0000), p.416 e p.420
(Registro 9999), em ~6–12 s.

## Consequências

- Reingerir é necessário para aproveitar normalização/segmentação; a busca híbrida vale também
  para gerações antigas (a coluna é gerada).
- `score` não é mais a chave de ordenação: um trecho pode vir à frente de outro com cosseno
  maior quando a correspondência lexical o favorece.
- O conjunto de avaliação é de desenvolvimento e não substitui o corpus/rubrica do responsável
  fiscal (T069) nem a avaliação oficial (T077). Pool, k e regras de sumário não foram calibrados
  fora deste guia.

## Evidência

Testes `SegmentationQualityTests` (3) e `KnowledgeQueryTests` (1); suítes completas Api.RAG
100/100 e Api.Agente 130/130; `dotnet format --verify-no-changes` limpo; ingestão real do Guia
1.35 com o perfil v5 e avaliação acima.
