-- Postgres schema for AIM Online Race Studio (from docs/schema-postgres.sql)
-- CSV stored by storage_key (volume path) so we can stream without buffering 120MB.

CREATE TABLE IF NOT EXISTS xrk_files (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  file_hash     VARCHAR(64) NOT NULL UNIQUE,
  filename      VARCHAR(512),
  library_date  VARCHAR(64),
  library_time  VARCHAR(64),
  vehicle       VARCHAR(512),
  track         VARCHAR(512),
  racer         VARCHAR(512),
  logger_id     BIGINT,
  lap_count     INT NOT NULL DEFAULT 0,
  date_created  TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_modified TIMESTAMPTZ NOT NULL DEFAULT now(),
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_xrk_files_hash ON xrk_files(file_hash);
CREATE INDEX IF NOT EXISTS idx_xrk_files_created_at ON xrk_files(created_at DESC);

CREATE TABLE IF NOT EXISTS xrk_laps (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  file_id       UUID NOT NULL REFERENCES xrk_files(id) ON DELETE CASCADE,
  lap_index     INT NOT NULL,
  start_sec     DOUBLE PRECISION NOT NULL,
  duration_sec  DOUBLE PRECISION NOT NULL,
  date_created  TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_modified TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(file_id, lap_index)
);

CREATE INDEX IF NOT EXISTS idx_xrk_laps_file_id ON xrk_laps(file_id);

CREATE TABLE IF NOT EXISTS xrk_channels (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  file_id       UUID NOT NULL REFERENCES xrk_files(id) ON DELETE CASCADE,
  channel_index INT NOT NULL,
  name          VARCHAR(256) NOT NULL,
  units         VARCHAR(128),
  date_created  TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_modified TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(file_id, channel_index)
);

CREATE INDEX IF NOT EXISTS idx_xrk_channels_file_id ON xrk_channels(file_id);

CREATE TABLE IF NOT EXISTS xrk_gps_channels (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  file_id       UUID NOT NULL REFERENCES xrk_files(id) ON DELETE CASCADE,
  name          VARCHAR(256) NOT NULL,
  date_created  TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_modified TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE(file_id, name)
);

CREATE INDEX IF NOT EXISTS idx_xrk_gps_channels_file_id ON xrk_gps_channels(file_id);

-- CSV: store by storage_key (path under volume) so backend can stream to file without buffering.
CREATE TABLE IF NOT EXISTS xrk_csv (
  file_id       UUID PRIMARY KEY REFERENCES xrk_files(id) ON DELETE CASCADE,
  storage_key   VARCHAR(512) NOT NULL,
  content_type  VARCHAR(64) DEFAULT 'text/csv',
  byte_size     BIGINT,
  date_created  TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_modified TIMESTAMPTZ NOT NULL DEFAULT now()
);
