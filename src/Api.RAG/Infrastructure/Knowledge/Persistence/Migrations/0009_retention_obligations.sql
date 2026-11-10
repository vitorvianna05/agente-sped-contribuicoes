CREATE TABLE rag.retention_obligations (
    artifact_id uuid NOT NULL REFERENCES rag.retained_artifacts,
    event_type text NOT NULL CHECK (event_type IN ('withdrawn','superseded')),
    event_at timestamptz NOT NULL,
    policy_version text NULL,
    retention_days integer NULL CHECK (retention_days > 0),
    eligible_at timestamptz NULL,
    PRIMARY KEY (artifact_id,event_type),
    CHECK ((retention_days IS NULL)=(eligible_at IS NULL)),
    CHECK (eligible_at IS NULL OR (policy_version IS NOT NULL AND eligible_at>=event_at))
);
INSERT INTO rag.retention_obligations
SELECT artifact_id,event_type,event_at,policy_version,retention_days,eligible_at FROM rag.retained_artifacts;
-- Uma retirada anterior sem prazo próprio comprovado permanece pendente, sem retroagir aprovação.
INSERT INTO rag.retention_obligations
SELECT a.artifact_id,'withdrawn',d.withdrawn_at,NULL,NULL,NULL
FROM rag.retained_artifacts a JOIN rag.document_versions d USING(document_version_id)
WHERE a.category='generation' AND a.event_type='superseded' AND d.withdrawn_at IS NOT NULL AND a.purged_at IS NULL
ON CONFLICT DO NOTHING;
