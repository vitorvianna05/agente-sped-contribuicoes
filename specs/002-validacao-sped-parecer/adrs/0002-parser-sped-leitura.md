# ADR 0002 — Leitura estrutural SPED baseada no guia fornecido

Data: 2026-11-10. Autorização: adicionar parser para leitura; o usuário forneceu o Guia
Prático EFD Contribuições 1.35 de 18/06/2021 como documentação. Complementa AG005/AG006 de
forma incremental, sem antecipar aceite de regras fiscais ou política de dados de AG001.

## Decisão

ISpedParser/SpedParser leem Stream assincronamente em blocos, sem seek e sem fechar o stream
do chamador. Latin-1 é explícito, conforme página física 33. Não detectar/converter UTF-8
silenciosamente; BOM UTF-8/UTF-16/UTF-32 e charset declarado incompatível são recusados.
Sem BOM, bytes são interpretados como Latin-1; não se alega detectar toda codificação errada.

Preservar ordem, linha física, REG, campos vazios intermediários/finais, zeros, espaços,
aspas e texto original da linha. Não é CSV: aspas/escape não alteram o significado de pipe.
Campos ficam como string, sem conversões locais de decimal, data ou identificador. GetField
usa a posição do guia (01=REG, 02=Fields[0]). CRLF é verificado sem normalizar a origem;
LF e EOF sem CRLF são lidos com ocorrência e nunca declarados leitura válida.

Domínio modela SpedRecord, SpedDocument/SpedReadIssue e SpedFileStructure. Este último
confere abertura/encerramento, formato de COD_VER e quantidade declarada de linhas. Não
contém HTTP, arquivos, SDK ou regras tributárias. O adaptador trata stream, bytes, hash e
limites. Registros sintaticamente legíveis mas não catalogados são preservados, sem afirmar
compatibilidade de leiaute. Valores não são comandos nem alteram configuração/permissões.

MaxRequestBytes limita entrada e arquivo (inclusive sem Content-Length). SpedParser tem
limites explícitos para bytes da linha (sem CRLF), registros, campos (incluindo REG) e
ocorrências. Excesso interrompe com erro, sem devolver truncamento como sucesso. O hash
SHA-256 cobre os bytes originais inclusive terminadores, calculado durante a leitura.

## HTTP e proteção de dados

POST /api/v1/sped/leitura exige analysis.execute e recebe bytes diretamente no corpo como
text/plain ou application/octet-stream. Não aceita caminho de arquivo nem busca URL fornecida
pelo usuário. Sem multipart/IFormFile para evitar buffering em arquivo temporário. HTTPS,
hosts, JWT normal ou Development loopback autorizado, timeout/cancelamento e IDs de execução/
correlação do sistema são os existentes. HTTP 200 pode conter readingStatus=invalid e
status=failed: o processamento de leitura retornou diagnóstico, não arquivo fiscal válido.
Limite excedido/encoding/vazio falham por HTTP e não retornam registros parciais.

A resposta contém dados do arquivo do próprio chamador, protegida pela mesma permissão e
Cache-Control:no-store. Não há persistência, log de linhas/valores, MCP, OpenAI ou envio a
terceiros. Linhas ilegíveis são rejeitadas com posição/código e RejectedLineCount; linhas
legíveis permanecem no resultado e não são confundidas com arquivo integralmente válido.

## Limites de aceite

A leitura concluída produz status=partial, fiscalValidationStatus=not_performed e lista de
verificações não realizadas. Não identifica automaticamente leiaute suportado, não valida
CNPJ, período, CST/CFOP, bases/alíquotas/créditos ou hierarquia dos blocos, não executa parecer
nem conclui AG006. Fontes/páginas/hash e limites de suporte em validation/supported-layouts.md.
AG001/AG005/AG006 continuam abertas; fornecimento do PDF não é aprovação fiscal integral.
Fixtures são sintéticas, sem afirmar que exemplos abreviados sejam escrituração válida.
