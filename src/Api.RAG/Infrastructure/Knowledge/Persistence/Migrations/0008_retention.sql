ALTER TABLE rag.document_versions ALTER COLUMN original DROP NOT NULL;
ALTER TABLE rag.chunks ALTER COLUMN text DROP NOT NULL;
CREATE TABLE rag.retained_artifacts (
    artifact_id uuid PRIMARY KEY,
    document_version_id uuid NOT NULL REFERENCES rag.document_versions,
    generation_id uuid NULL REFERENCES rag.generations,
    category text NOT NULL CHECK (category IN ('generation','original')),
    event_type text NOT NULL CHECK (event_type IN ('withdrawn','superseded')),
    event_at timestamptz NOT NULL,
    policy_version text NULL,
    eligible_at timestamptz NULL,
    purged_at timestamptz NULL,
    revision uuid NOT NULL,
    purged_by text NULL,
    expurge_reason text NULL,
    retention_days integer NULL CHECK (retention_days > 0),
    UNIQUE (generation_id),
    CHECK (eligible_at IS NULL OR eligible_at >= event_at)
);
CREATE UNIQUE INDEX retained_original_once ON rag.retained_artifacts(document_version_id) WHERE category='original';
CREATE TABLE rag.reference_tombstones (
    reference_id uuid PRIMARY KEY,
    purged_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL CHECK (expires_at > purged_at)
);
CREATE TABLE rag.command_results (
    operation_id uuid PRIMARY KEY REFERENCES rag.operations,
    response jsonb NOT NULL
);
