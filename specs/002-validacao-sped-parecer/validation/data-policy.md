# Política de dados — pendências e comportamento técnico

Data: 2026-11-10. AG001/AG011 não recebem aprovação de política por este documento.

Resposta do usuário: “Vou informar os prazos e a referência da aprovação; configurarei a
conexão localmente”. Nenhum prazo, aprovador, referência real ou conexão foi informado.

- Arquivo SPED, texto de evidências, prompt e parecer: apenas memória durante a requisição;
  não persistidos por este incremento. Respostas no-store, sem arquivo temporário.
- Antes de analisar: AgentData.ConnectionString, PolicyVersion, ApprovalReference, AuditDays
  e ConsumptionDays são obrigatórios; ausentes produzem AUDIT_NOT_CONFIGURED antes de MCP/IA.
  Isso não bloqueia leitura estrutural sem persistência em /api/v1/sped/leitura.
- PostgreSQL próprio guarda execução/correlação, subject/organization_id do transporte,
  estado/código e tentativa/modelo/prompt/timestamps/uso disponível. Guarda também operação,
  correlação remota, estado e IDs de referências das respostas MCP válidas; não guarda payloads/textos.
- Auditoria deve durar pelo menos tanto quanto consumo, pois mantém seu vínculo de dono e
  correlação. Prazos reais precisam respeitar essa restrição ou exigir revisão do desenho.
- expires_at é calculado na gravação, junto da política/referência. Consultas não retornam
  linhas vencidas. --purge-agent remove vencidos em lotes, filhos antes dos pais, por papel
  administrativo separado. Exige política preenchida; não foi agendado nem rodado em banco real.
- A versão/configuração posterior não estende ou reduz silenciosamente as datas gravadas.
  Mudanças retroativas, suspensão legal e retenção de resultados exigem desenho/aprovação próprios.
- Uso/preço/faturamento desconhecido fica NULL. A configuração não possui preços presumidos;
  relatório não afirma custo zero. Os testes usam prazos e aprovação expressamente sintéticos.

Ainda necessário: prazos/referência/responsáveis reais; definição da política para envio do
contexto mínimo ao provedor; infraestrutura/segredos locais; rotina operacional de expurgo e
tratamento de intenções pending após queda. Não há aceite de tratamento de dados reais.
