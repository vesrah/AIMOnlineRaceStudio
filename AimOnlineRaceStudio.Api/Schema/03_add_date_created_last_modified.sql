-- Add date_created and last_modified to all tables (idempotent).
-- xrk_files: backfill from created_at for existing rows.
ALTER TABLE xrk_files ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ;
ALTER TABLE xrk_files ADD COLUMN IF NOT EXISTS last_modified TIMESTAMPTZ;
UPDATE xrk_files SET date_created = created_at WHERE date_created IS NULL;
UPDATE xrk_files SET last_modified = COALESCE(last_modified, created_at) WHERE last_modified IS NULL;
ALTER TABLE xrk_files ALTER COLUMN date_created SET DEFAULT now();
ALTER TABLE xrk_files ALTER COLUMN last_modified SET DEFAULT now();
UPDATE xrk_files SET date_created = now() WHERE date_created IS NULL;
UPDATE xrk_files SET last_modified = now() WHERE last_modified IS NULL;
ALTER TABLE xrk_files ALTER COLUMN date_created SET NOT NULL;
ALTER TABLE xrk_files ALTER COLUMN last_modified SET NOT NULL;
CREATE INDEX IF NOT EXISTS idx_xrk_files_last_modified ON xrk_files(last_modified DESC);

-- Child tables: add columns (nullable first), backfill, then set NOT NULL and DEFAULT.
-- xrk_laps
ALTER TABLE xrk_laps ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ;
ALTER TABLE xrk_laps ADD COLUMN IF NOT EXISTS last_modified TIMESTAMPTZ;
UPDATE xrk_laps SET date_created = now() WHERE date_created IS NULL;
UPDATE xrk_laps SET last_modified = now() WHERE last_modified IS NULL;
ALTER TABLE xrk_laps ALTER COLUMN date_created SET DEFAULT now();
ALTER TABLE xrk_laps ALTER COLUMN last_modified SET DEFAULT now();
ALTER TABLE xrk_laps ALTER COLUMN date_created SET NOT NULL;
ALTER TABLE xrk_laps ALTER COLUMN last_modified SET NOT NULL;

-- xrk_channels
ALTER TABLE xrk_channels ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ;
ALTER TABLE xrk_channels ADD COLUMN IF NOT EXISTS last_modified TIMESTAMPTZ;
UPDATE xrk_channels SET date_created = now() WHERE date_created IS NULL;
UPDATE xrk_channels SET last_modified = now() WHERE last_modified IS NULL;
ALTER TABLE xrk_channels ALTER COLUMN date_created SET DEFAULT now();
ALTER TABLE xrk_channels ALTER COLUMN last_modified SET DEFAULT now();
ALTER TABLE xrk_channels ALTER COLUMN date_created SET NOT NULL;
ALTER TABLE xrk_channels ALTER COLUMN last_modified SET NOT NULL;

-- xrk_gps_channels
ALTER TABLE xrk_gps_channels ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ;
ALTER TABLE xrk_gps_channels ADD COLUMN IF NOT EXISTS last_modified TIMESTAMPTZ;
UPDATE xrk_gps_channels SET date_created = now() WHERE date_created IS NULL;
UPDATE xrk_gps_channels SET last_modified = now() WHERE last_modified IS NULL;
ALTER TABLE xrk_gps_channels ALTER COLUMN date_created SET DEFAULT now();
ALTER TABLE xrk_gps_channels ALTER COLUMN last_modified SET DEFAULT now();
ALTER TABLE xrk_gps_channels ALTER COLUMN date_created SET NOT NULL;
ALTER TABLE xrk_gps_channels ALTER COLUMN last_modified SET NOT NULL;

-- xrk_csv
ALTER TABLE xrk_csv ADD COLUMN IF NOT EXISTS date_created TIMESTAMPTZ;
ALTER TABLE xrk_csv ADD COLUMN IF NOT EXISTS last_modified TIMESTAMPTZ;
UPDATE xrk_csv SET date_created = now() WHERE date_created IS NULL;
UPDATE xrk_csv SET last_modified = now() WHERE last_modified IS NULL;
ALTER TABLE xrk_csv ALTER COLUMN date_created SET DEFAULT now();
ALTER TABLE xrk_csv ALTER COLUMN last_modified SET DEFAULT now();
ALTER TABLE xrk_csv ALTER COLUMN date_created SET NOT NULL;
ALTER TABLE xrk_csv ALTER COLUMN last_modified SET NOT NULL;

-- Trigger to set last_modified = now() on UPDATE.
CREATE OR REPLACE FUNCTION set_last_modified()
RETURNS TRIGGER AS $$
BEGIN
  NEW.last_modified = now();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_xrk_files_last_modified ON xrk_files;
CREATE TRIGGER tr_xrk_files_last_modified
  BEFORE UPDATE ON xrk_files FOR EACH ROW EXECUTE FUNCTION set_last_modified();

DROP TRIGGER IF EXISTS tr_xrk_laps_last_modified ON xrk_laps;
CREATE TRIGGER tr_xrk_laps_last_modified
  BEFORE UPDATE ON xrk_laps FOR EACH ROW EXECUTE FUNCTION set_last_modified();

DROP TRIGGER IF EXISTS tr_xrk_channels_last_modified ON xrk_channels;
CREATE TRIGGER tr_xrk_channels_last_modified
  BEFORE UPDATE ON xrk_channels FOR EACH ROW EXECUTE FUNCTION set_last_modified();

DROP TRIGGER IF EXISTS tr_xrk_gps_channels_last_modified ON xrk_gps_channels;
CREATE TRIGGER tr_xrk_gps_channels_last_modified
  BEFORE UPDATE ON xrk_gps_channels FOR EACH ROW EXECUTE FUNCTION set_last_modified();

DROP TRIGGER IF EXISTS tr_xrk_csv_last_modified ON xrk_csv;
CREATE TRIGGER tr_xrk_csv_last_modified
  BEFORE UPDATE ON xrk_csv FOR EACH ROW EXECUTE FUNCTION set_last_modified();
