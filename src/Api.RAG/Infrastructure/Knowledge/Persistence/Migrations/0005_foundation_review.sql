ALTER TABLE rag.operations ADD CONSTRAINT operation_finish_not_before_creation
    CHECK (finished_at IS NULL OR finished_at >= created_at);

-- currency representa somente a moeda informada pelo provedor.
-- A moeda da estimativa é independente; não presumir conversão nem moeda de valores históricos.
ALTER TABLE rag.consumption_attempts ADD COLUMN estimated_currency text NULL;
