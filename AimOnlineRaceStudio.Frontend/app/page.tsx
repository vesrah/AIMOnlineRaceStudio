import Link from 'next/link';
import nextDynamic from 'next/dynamic';
import { listFiles } from '@/lib/api';
import { formatDate, formatLapTime, formatSessionDateTime } from '@/lib/format';
import { groupFilesByTrack } from '@/lib/grouping';
import { getErrorMessage } from '@/lib/utils';
import type { XrkFileSummary } from '@/types/api';

const DeleteFileButton = nextDynamic(
  () => import('@/app/components/DeleteFileButton').then((m) => ({ default: m.DeleteFileButton })),
  { ssr: true }
);

export const dynamic = 'force-dynamic';

export default async function HomePage() {
  let files: XrkFileSummary[] = [];
  let error: string | null = null;

  try {
    files = await listFiles();
  } catch (e) {
    error = getErrorMessage(e, 'Failed to load files');
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

      {!error && files.length > 0 && (() => {
        const groups = groupFilesByTrack(files);
        const colCount = 8;
        return (
          <table className="table-files">
            <thead>
              <tr>
                <th>Filename</th>
                <th>Vehicle</th>
                <th>Track</th>
                <th>Laps</th>
                <th>Session start</th>
                <th>Fastest Lap</th>
                <th>Created</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {groups.flatMap(({ track, files: groupFiles }) => [
                <tr key={`track-${track}`} className="track-group-header">
                  <td colSpan={colCount}><strong>{track}</strong></td>
                </tr>,
                ...groupFiles.map((f) => (
                  <tr key={f.id}>
                    <td>
                      <Link href={`/files/${f.id}`}>
                        {f.filename || '(unnamed)'}
                      </Link>
                    </td>
                    <td>{f.vehicle ?? '—'}</td>
                    <td>{f.track ?? '—'}</td>
                    <td>{f.lap_count > 2 ? f.lap_count - 2 : '—'}</td>
                    <td>{formatSessionDateTime(f.library_date, f.library_time) ?? formatDate(f.created_at)}</td>
                    <td>
                      {f.shortest_middle_lap_sec != null
                        ? formatLapTime(f.shortest_middle_lap_sec)
                        : '—'}
                    </td>
                    <td>{formatDate(f.created_at)}</td>
                    <td>
                      <DeleteFileButton fileId={f.id} />
                    </td>
                  </tr>
                )),
              ])}
            </tbody>
          </table>
        );
      })()}
    </>
  );
}
