-- Add logger_id to xrk_files for existing databases (idempotent).
ALTER TABLE xrk_files ADD COLUMN IF NOT EXISTS logger_id BIGINT;
