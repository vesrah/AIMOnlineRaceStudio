/**
 * API types aligned with backend REST and docs/schema-postgres.sql, docs/xrk-metadata-example.json
 */

export interface XrkFileSummary {
  id: string;
  file_hash: string;
  filename: string | null;
  vehicle: string | null;
  track: string | null;
  created_at: string;
  /** Shortest lap duration (seconds) among laps that are not first or last; undefined if none. */
  shortest_middle_lap_sec?: number | null;
}

/** Lap from file detail (matches xrk_laps / metadata.laps) */
export interface XrkLap {
  lap_index: number;
  start_sec: number;
  duration_sec: number;
}

/** Channel from file detail (matches xrk_channels / metadata.channels) */
export interface XrkChannel {
  channel_index: number;
  name: string;
  units: string | null;
}

export interface XrkFileDetail extends XrkFileSummary {
  library_date?: string | null;
  library_time?: string | null;
  racer?: string | null;
  lap_count: number;
  laps: XrkLap[];
  channels: XrkChannel[];
  gps_channels?: string[];
}

export interface UploadSuccessResponse {
  id: string;
  filename?: string;
  message?: string;
}

export interface ApiError {
  message?: string;
  status?: number;
}
