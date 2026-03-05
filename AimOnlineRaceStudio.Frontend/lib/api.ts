/**
 * Typed API client for the backend (BFF).
 * Base URL from NEXT_PUBLIC_API_URL. In dev, set NEXT_PUBLIC_USE_API_PROXY=true
 * to proxy /api to the backend and avoid CORS.
 */

import type {
  XrkFileSummary,
  XrkFileDetail,
  UploadSuccessResponse,
} from '@/types/api';

/** Normalize list item from backend (accept camelCase or snake_case) */
function normalizeFileSummary(raw: Record<string, unknown>): XrkFileSummary {
  const shortest =
    raw.shortest_middle_lap_sec ?? (raw as Record<string, unknown>).shortestMiddleLapSec;
  const r = raw as Record<string, unknown>;
  return {
    id: String(raw.id ?? ''),
    file_hash: String(raw.file_hash ?? r.fileHash ?? ''),
    filename: raw.filename != null ? String(raw.filename) : null,
    vehicle: raw.vehicle != null ? String(raw.vehicle) : null,
    track: raw.track != null ? String(raw.track) : null,
    created_at: String(raw.created_at ?? r.createdAt ?? ''),
    shortest_middle_lap_sec:
      shortest != null && typeof shortest === 'number' ? shortest : undefined,
    library_date: (raw.library_date ?? r.libraryDate ?? r.LibraryDate) != null ? String(raw.library_date ?? r.libraryDate ?? r.LibraryDate) : null,
    library_time: (raw.library_time ?? r.libraryTime ?? r.LibraryTime) != null ? String(raw.library_time ?? r.libraryTime ?? r.LibraryTime) : null,
    logger_id: (raw.logger_id ?? r.loggerId ?? r.LoggerId) != null ? Number(raw.logger_id ?? r.loggerId ?? r.LoggerId) : null,
    lap_count: Number(raw.lap_count ?? r.lapCount ?? 0),
  };
}

function getBaseUrl(): string {
  if (typeof window === 'undefined') {
    // Server (e.g. in Docker): use API_SERVER_URL to reach backend by service name (http://backend:8080).
    // Otherwise fall back to NEXT_PUBLIC_API_URL (e.g. when running locally).
    const serverUrl = process.env.API_SERVER_URL || process.env.NEXT_PUBLIC_API_URL || '';
    return serverUrl.replace(/\/$/, '');
  }
  // Client (browser): use NEXT_PUBLIC_API_URL so the browser hits the backend (e.g. http://localhost:5001).
  // With proxy, use same origin.
  const useProxy = process.env.NEXT_PUBLIC_USE_API_PROXY === 'true';
  if (useProxy) return '';
  return (process.env.NEXT_PUBLIC_API_URL || '').replace(/\/$/, '');
}

const base = () => getBaseUrl();
const api = (path: string) => `${base()}/api${path}`;

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = res.statusText;
    const text = await res.text();
    if (text) {
      try {
        const body = JSON.parse(text) as { message?: string; error?: string };
        message = body.message ?? body.error ?? text;
      } catch {
        message = text;
      }
    }
    throw new Error(message);
  }
  return res.json();
}

export async function listFiles(): Promise<XrkFileSummary[]> {
  const url = api('/files');
  const res = await fetch(url, { cache: 'no-store' });
  const data = await handleResponse<unknown[]>(res);
  return (data ?? []).map((item) => normalizeFileSummary(item as Record<string, unknown>));
}

function normalizeFileDetail(raw: Record<string, unknown>): XrkFileDetail {
  const summary = normalizeFileSummary(raw);
  const laps = Array.isArray(raw.laps)
    ? (raw.laps as Record<string, unknown>[]).map((l) => ({
        lap_index: Number(l.lap_index ?? l.lapIndex ?? l.index ?? (l as Record<string, unknown>).Index ?? 0),
        start_sec: Number(l.start_sec ?? l.startSec ?? l.start ?? (l as Record<string, unknown>).Start ?? 0),
        duration_sec: Number(l.duration_sec ?? l.durationSec ?? l.duration ?? (l as Record<string, unknown>).Duration ?? 0),
      }))
    : [];
  const channels = Array.isArray(raw.channels)
    ? (raw.channels as Record<string, unknown>[]).map((c) => ({
        channel_index: Number(c.channel_index ?? c.channelIndex ?? c.index ?? (c as Record<string, unknown>).Index ?? 0),
        name: String(c.name ?? (c as Record<string, unknown>).Name ?? ''),
        units: c.units != null ? String(c.units) : (c as Record<string, unknown>).Units != null ? String((c as Record<string, unknown>).Units) : null,
      }))
    : [];
  return {
    ...summary,
    library_date: raw.library_date != null ? String(raw.library_date) : (raw as Record<string, unknown>).libraryDate != null ? String((raw as Record<string, unknown>).libraryDate) : undefined,
    library_time: raw.library_time != null ? String(raw.library_time) : (raw as Record<string, unknown>).libraryTime != null ? String((raw as Record<string, unknown>).libraryTime) : undefined,
    racer: raw.racer != null ? String(raw.racer) : null,
    logger_id: (raw.logger_id ?? (raw as Record<string, unknown>).loggerId) != null ? Number(raw.logger_id ?? (raw as Record<string, unknown>).loggerId) : null,
    lap_count: Number(raw.lap_count ?? (raw as Record<string, unknown>).lapCount ?? 0),
    laps,
    channels,
    gps_channels: Array.isArray(raw.gps_channels) ? (raw.gps_channels as string[]) : Array.isArray((raw as Record<string, unknown>).gpsChannels) ? ((raw as Record<string, unknown>).gpsChannels as string[]) : undefined,
  };
}

export async function getFile(id: string): Promise<XrkFileDetail> {
  const url = api(`/files/${encodeURIComponent(id)}`);
  const res = await fetch(url, { cache: 'no-store' });
  const data = await handleResponse<Record<string, unknown>>(res);
  return normalizeFileDetail(data);
}

/**
 * Returns the URL to fetch CSV for a file (GET). Use for download or streaming.
 */
export function getCsvUrl(id: string): string {
  return api(`/files/${encodeURIComponent(id)}/csv`);
}

/**
 * Delete a file. Throws on error; 204 No Content on success.
 */
export async function deleteFile(id: string): Promise<void> {
  const url = api(`/files/${encodeURIComponent(id)}`);
  const res = await fetch(url, { method: 'DELETE', cache: 'no-store' });
  if (!res.ok) {
    let message = res.statusText;
    const text = await res.text();
    if (text) {
      try {
        const body = JSON.parse(text) as { message?: string; error?: string };
        message = body.message ?? body.error ?? text;
      } catch {
        message = text;
      }
    }
    throw new Error(message);
  }
}

/**
 * Upload XRK file (multipart/form-data). Returns file id and optional message.
 */
export async function uploadFile(file: File): Promise<UploadSuccessResponse> {
  const url = api('/files/upload');
  const form = new FormData();
  form.append('file', file);
  const res = await fetch(url, {
    method: 'POST',
    body: form,
  });
  return handleResponse<UploadSuccessResponse>(res);
}

/** Debug: XrkApi health from backend proxy (GET /api/debug/xrkapi-health) */
export interface XrkApiHealthDebug {
  configuredUrl: string;
  result: {
    reachable: boolean;
    statusCode?: number;
    ok?: boolean;
    body?: Record<string, unknown>;
    error?: string;
  };
}

export async function getXrkApiHealth(): Promise<XrkApiHealthDebug> {
  const url = api('/debug/xrkapi-health');
  const res = await fetch(url, { cache: 'no-store' });
  return handleResponse<XrkApiHealthDebug>(res);
}

/** Debug: storage stats (total size of uploaded CSVs and file count) */
export interface StorageStats {
  totalBytes: number;
  fileCount: number;
}

export async function getStorageStats(): Promise<StorageStats> {
  const url = api('/debug/storage');
  const res = await fetch(url, { cache: 'no-store' });
  return handleResponse<StorageStats>(res);
}

export async function clearXrkApiCache(): Promise<{ cleared: number }> {
  const url = api('/debug/cache');
  const res = await fetch(url, { method: 'DELETE', cache: 'no-store' });
  return handleResponse<{ cleared: number }>(res);
}

/** Delete all files from the database and disk, clear XrkApi cache. Returns count of files removed. */
export async function clearAllData(): Promise<{ deleted: number }> {
  const url = api('/debug/clear-all');
  const res = await fetch(url, { method: 'POST', cache: 'no-store' });
  return handleResponse<{ deleted: number }>(res);
}
