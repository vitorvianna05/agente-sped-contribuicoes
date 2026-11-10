# Fontes recebidas e limites de suporte

Registro: 2026-11-10. Documento fornecido pelo usuário como referência técnica para leitura.
Este registro cobre parte de AG001; não designa aprovador fiscal, política de dados ou
compatibilidade integral de leiaute, e não encerra AG001/AG005/AG006.

## Referência recebida

- Arquivo: Guia_Pratico_EFD_Contribuicoes_Versao_1_35 - 18_06_2021.pdf.
- Título/edição observados: Guia Prático da EFD Contribuições, versão 1.35, atualização
  18/06/2021. A versão do guia não é o código COD_VER do arquivo SPED.
- 433 páginas físicas; tamanho observado: 4.105.830 bytes.
- SHA-256: 60eace459169238808e3e745e952fcbbbd2af6b7f933b043e8c9692b68f4d08b.
- Origem nesta sessão: arquivo local indicado pelo usuário; não houve download oficial ou
  confirmação externa de URL/atualidade/aplicabilidade ao período fiscal do arquivo.
- PDF consultado localmente com PdfPig; não foi copiado ao Git, ingerido na RAG ou enviado
  à OpenAI. Nenhum arquivo SPED real foi fornecido ou usado nos testes.

## Fundamentação da leitura implementada

| Página física | Seção | Aplicação técnica |
| --- | --- | --- |
| 33 | Seção 2.1, características do arquivo digital | Texto Latin-1 (ISO 8859-1), registros na primeira coluna, delimitador pipe no início e após cada campo, CRLF e preservação de campos vazios. |
| 34 | Seção 3, preenchimento de campos | Não converter automaticamente valores numéricos, datas, códigos ou zeros; detectar caracteres de controle na leitura. Validação de tipo/tamanho de cada campo não foi implementada. |
| 66 | Registro 0000 | Identificar registro de abertura; extrair posição 02 como COD_VER declarado, com três dígitos. Não inferir versão pelo nome do arquivo ou pela edição do PDF. |
| 416 | Registro 9999 | Registro de encerramento único/final; ler posição 02 como quantidade total de linhas, incluindo o próprio 9999, e confrontar com linhas físicas observadas. |

## Suporte e pendências

- Implementado: reconhecimento lexical e leitura estrutural genérica, sem catálogo de
  campos por tipo de registro. REG de quatro caracteres ASCII maiúsculos/dígitos é preservado;
  sua existência num leiaute fiscal não é confirmada por essa leitura.
- COD_VER é apenas declaração do arquivo. Nenhum código foi cadastrado como integralmente
  suportado ou fiscalmente aprovado. A compatibilidade segue not_assessed na resposta.
- Pendente: tabela versionada de leiautes por período, identificação/validação semântica
  dos registros e campos, hierarquia/contadores de blocos, regras fiscais, corpus oficial
  autorizado e revisão do responsável fiscal.
- AG001 também exige política de dados/retensão de arquivos/resultados; não foi inventada.
  A leitura é transitória em memória, sem arquivo temporário ou persistência e com no-store.

Contratos e resultados: [parser-sped.md](../contracts/parser-sped.md).

## Esclarecimento e incremento de análise

O usuário solicitou períodos de 2020 até 2026. Isso delimita o escopo desejado, sem converter
anos em COD_VER. A tabela oficial código × vigência e tipoDocumento da RAG continuam sem
definição/aprovação. LayoutPolicy.Profiles permanece vazio nos exemplos reais. Perfil exige
código de três dígitos, intervalo inclusivo dentro de 2020–2026, tipo/versão do conhecimento,
fonte e referência de aprovação; sobreposição do mesmo código é rejeitada.

SpedValidator agora lê DT_INI/DT_FIN nas posições 06/07 do 0000, conforme p.66, verifica datas
e ordem do período e consulta somente o perfil exato. Ausência de perfil produz unsupported,
sem MCP ou IA. Essa leitura não cobre todas as restrições de período da escrituração.
Os testes usam 006 + synthetic-type/synthetic-layout e aprovação sintética; isso não é
evidência de compatibilidade oficial de 006 para todos os anos. Campos específicos/hierarquia/
regras fiscais permanecem não realizados. O fluxo de parecer retorna sempre cobertura parcial.
[Decisão](../adrs/0003-fluxo-parecer-auditoria.md), [política pendente](data-policy.md).
