CREATE TABLE rag.audit_entries (
    audit_id uuid PRIMARY KEY,
    principal_id text NOT NULL CHECK (length(principal_id) > 0),
    organization_id text NULL,
    action text NOT NULL CHECK (length(action) > 0),
    object_id uuid NULL,
    result text NOT NULL CHECK (length(result) > 0),
    occurred_at timestamptz NOT NULL,
    correlation_id text NOT NULL CHECK (length(correlation_id) > 0)
);
CREATE INDEX audit_principal_time ON rag.audit_entries(principal_id, occurred_at);
