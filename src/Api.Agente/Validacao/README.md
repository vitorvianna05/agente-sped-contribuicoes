ISpedParser/SpedParser implementam leitura estrutural streaming em Latin-1, preservando
registros/campos e verificando envelope/CRLF, com limites e cancelamento. Endpoint protegido:
POST /api/v1/sped/leitura, arquivo diretamente no corpo. Não há persistência, MCP ou IA.

A referência fornecida é o Guia EFD Contribuições 1.35 (18/06/2021), páginas físicas
33/34/66/416; registro da fonte em specs/002-validacao-sped-parecer/validation/supported-layouts.md.
COD_VER é declarado pelo arquivo e não representa leiaute integralmente suportado.
AG005/AG006 ainda exigem catálogo, semântica/hierarquia e regras fiscais aprovadas em AG001.

SpedValidator verifica datas 06/07 do 0000 e mapeamento inequívoco LayoutPolicy.Profiles
para períodos 2020–2026. Perfil sem fonte/aprovação ou sobreposto é inválido; exemplos reais
ficam sem perfis até definir COD_VER/vigência e contexto documental na RAG. Não há inferência
de suporte por edição do PDF. No fluxo de análise, cobertura permanece parcial com revisão humana.
