import Link from 'next/link';
import { listFiles } from '@/lib/api';
import { formatLapTime } from '@/lib/format';
import type { XrkFileSummary } from '@/types/api';

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: 'short',
      timeStyle: 'short',
    });
  } catch {
    return iso;
  }
}

export const dynamic = 'force-dynamic';

export default async function HomePage() {
  let files: XrkFileSummary[] = [];
  let error: string | null = null;

  try {
    files = await listFiles();
  } catch (e) {
    const err = e as { message?: string };
    error = err?.message ?? 'Failed to load files';
  }

  return (
    <>
      <section className="page-header">
        <h2>Converted files</h2>
      </section>

      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}

      {!error && files.length === 0 && (
        <p>No files yet. <Link href="/upload">Upload an XRK file</Link> to get started.</p>
      )}

      {!error && files.length > 0 && (
        <table className="table-files">
          <thead>
            <tr>
              <th>Filename</th>
              <th>Vehicle</th>
              <th>Track</th>
              <th>Fastest Lap</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {files.map((f) => (
              <tr key={f.id}>
                <td>
                  <Link href={`/files/${f.id}`}>
                    {f.filename || '(unnamed)'}
                  </Link>
                </td>
                <td>{f.vehicle ?? '—'}</td>
                <td>{f.track ?? '—'}</td>
                <td>
                  {f.shortest_middle_lap_sec != null
                    ? formatLapTime(f.shortest_middle_lap_sec)
                    : '—'}
                </td>
                <td>{formatDate(f.created_at)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
