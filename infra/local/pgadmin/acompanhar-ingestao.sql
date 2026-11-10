-- Consultas para acompanhar a ingestão no pgAdmin (Query Tool no banco "rag").

-- 1. Operações mais recentes e seu estado.
SELECT o.operation_id, o.type, o.state, o.result_code, o.attempt_number,
       o.created_at, o.started_at, o.finished_at, o.lease_until
FROM rag.operations o
ORDER BY o.created_at DESC
LIMIT 20;

-- 2. Fonte -> versão documental -> geração (preparação textual e etapa vetorial).
SELECT s.issuer, s.official_identifier, v.official_version, v.publication_state,
       g.generation_id, g.state AS generation_state, g.preparation_state,
       g.embedding_state, g.embedding_attempts, g.failure_code, g.created_at
FROM rag.sources s
JOIN rag.document_versions v ON v.source_id = s.source_id
JOIN rag.generations g ON g.document_version_id = v.document_version_id
ORDER BY g.created_at DESC;

-- 3. Trechos gerados por geração (vetor nulo = embeddings pendentes).
SELECT c.generation_id, c.ordinal, left(c.text, 120) AS trecho,
       c.location -> 'physicalPages' AS paginas, c.embedding IS NOT NULL AS vetorizado
FROM rag.chunks c
ORDER BY c.created_at DESC, c.ordinal
LIMIT 100;

-- 4. Trilha de auditoria recente.
SELECT * FROM rag.audit_entries ORDER BY 1 DESC LIMIT 50;
