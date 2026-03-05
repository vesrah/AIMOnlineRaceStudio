import type { XrkFileSummary } from '@/types/api';

export interface FileGroupByTrack {
  track: string;
  files: XrkFileSummary[];
}

/**
 * Group files by track, sort track names (— last), and sort files within each group
 * by shortest middle lap ascending.
 */
export function groupFilesByTrack(files: XrkFileSummary[]): FileGroupByTrack[] {
  const trackKey = (f: XrkFileSummary) => f.track?.trim() || '—';
  const groups = new Map<string, XrkFileSummary[]>();
  for (const f of files) {
    const key = trackKey(f);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(f);
  }
  const sortedKeys = Array.from(groups.keys()).sort((a, b) =>
    a === '—' ? 1 : b === '—' ? -1 : a.localeCompare(b)
  );
  for (const key of sortedKeys) {
    const list = groups.get(key)!;
    list.sort((a, b) => {
      const sa = a.shortest_middle_lap_sec ?? Infinity;
      const sb = b.shortest_middle_lap_sec ?? Infinity;
      return sa - sb;
    });
  }
  return sortedKeys.map((track) => ({ track, files: groups.get(track)! }));
}
