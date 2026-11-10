OpenAiParecerLlm implementa a porta de Analise com gpt-4.1-mini configurável, SDK OpenAI 2.14.0
e Structured Outputs. Prompt/schema v1 são recursos versionados. Recebe resultados mínimos e
evidências como dados não confiáveis, sem arquivo completo. MeasuredParecerLlm coordena
intenção/auditoria por tentativa, timeout/retry finitos e validação do rascunho.

Testes usam HTTP/gerador simulados, sem chamada faturável. Qualidade da prosa exige AG014.
Configurar Llm externamente; nunca inserir chave nos JSON versionados. Ver contrato e ADR 0003
da Feature 002. Política/layouts reais continuam pendentes; não há conformidade fiscal automática.
