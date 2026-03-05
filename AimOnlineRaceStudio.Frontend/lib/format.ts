/** Format seconds as minutes:seconds:milliseconds (3 digits), e.g. 1:23:456 */
export function formatLapTime(seconds: number): string {
  const totalMs = Math.round(seconds * 1000);
  const mins = Math.floor(totalMs / 60000);
  const secs = Math.floor((totalMs % 60000) / 1000);
  const ms = totalMs % 1000;
  return `${mins}:${secs.toString().padStart(2, '0')}:${ms.toString().padStart(3, '0')}`;
}

/** Format ISO date string to locale short date/time. Falls back to raw string on parse error. */
export function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: 'short',
      timeStyle: 'short',
    });
  } catch {
    return iso;
  }
}

/**
 * Format session date/time from library_date (YYYY-MM-DD) and library_time (HH:mm:ss).
 * Returns null if missing or unparseable.
 */
export function formatSessionDateTime(
  libraryDate: string | null | undefined,
  libraryTime: string | null | undefined
): string | null {
  if (!libraryDate || !libraryTime) return null;
  try {
    const d = new Date(`${libraryDate}T${libraryTime}`);
    if (Number.isNaN(d.getTime())) return null;
    return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
  } catch {
    return null;
  }
}

/**
 * Lap start as actual clock time if session start (library_date + library_time) is available;
 * otherwise returns session-relative lap time (e.g. 0:02:345).
 */
export function formatLapStartActual(
  startSec: number,
  libraryDate: string | null | undefined,
  libraryTime: string | null | undefined
): string {
  if (libraryDate && libraryTime) {
    try {
      const sessionStart = new Date(`${libraryDate}T${libraryTime}`);
      if (!Number.isNaN(sessionStart.getTime())) {
        const actual = new Date(sessionStart.getTime() + startSec * 1000);
        return actual.toLocaleTimeString(undefined, {
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
          hour12: false,
        });
      }
    } catch {
      // fall through to relative
    }
  }
  return formatLapTime(startSec);
}

/** Format byte count as MB with 2 decimal places. */
export function formatMb(bytes: number): string {
  const mb = bytes / (1024 * 1024);
  return mb.toFixed(2);
}
