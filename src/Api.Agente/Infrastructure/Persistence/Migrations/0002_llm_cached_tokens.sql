ALTER TABLE agente.consumption_attempts ADD COLUMN cached_input_tokens bigint NULL
    CHECK (cached_input_tokens>=0 AND (input_tokens IS NULL OR cached_input_tokens<=input_tokens));
