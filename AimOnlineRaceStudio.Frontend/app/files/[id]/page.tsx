import Link from 'next/link';
import nextDynamic from 'next/dynamic';
import { notFound } from 'next/navigation';
import { getFile } from '@/lib/api';
import { formatLapTime, formatSessionDateTime } from '@/lib/format';

const DeleteFileButton = nextDynamic(
  () => import('@/app/components/DeleteFileButton').then((m) => ({ default: m.DeleteFileButton })),
  { ssr: true }
);

export const dynamic = 'force-dynamic';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function FileDetailPage({ params }: PageProps) {
  const { id } = await params;
  let detail;
  try {
    detail = await getFile(id);
  } catch {
    notFound();
  }

  return (
    <>
      <section className="page-header">
        <h2>{detail.filename || 'File details'}</h2>
      </section>

      <dl className="meta-grid">
        <dt>Vehicle</dt>
        <dd>{detail.vehicle ?? '—'}</dd>
        <dt>Track</dt>
        <dd>{detail.track ?? '—'}</dd>
        <dt>Racer</dt>
        <dd>{detail.racer ?? '—'}</dd>
        <dt>Logger serial</dt>
        <dd>{detail.logger_id != null ? String(detail.logger_id) : '—'}</dd>
        <dt>Laps</dt>
        <dd>{detail.lap_count}</dd>
        <dt>Session date</dt>
        <dd>{formatSessionDateTime(detail.library_date, detail.library_time) ?? '—'}</dd>
      </dl>

      {(() => {
        const middleLaps = detail.laps.length > 2 ? detail.laps.slice(1, -1) : [];
        return middleLaps.length > 0 ? (
          <>
            <h3>Laps</h3>
            <p className="muted">First and last laps (warmup / cooldown) are hidden.</p>
            <ul>
              {middleLaps.map((lap) => (
                <li key={lap.lap_index}>
                  Lap {lap.lap_index - 1}: {formatLapTime(lap.duration_sec)}
                </li>
              ))}
            </ul>
          </>
        ) : null;
      })()}

      {detail.channels.length > 0 && (
        <>
          <h3>Channels</h3>
          <ul>
            {detail.channels.map((ch) => (
              <li key={ch.channel_index}>
                {ch.name}{ch.units ? ` (${ch.units})` : ''}
              </li>
            ))}
          </ul>
        </>
      )}

      {detail.gps_channels && detail.gps_channels.length > 0 && (
        <>
          <h3>GPS channels</h3>
          <ul>
            {detail.gps_channels.map((name) => (
              <li key={name}>{name}</li>
            ))}
          </ul>
        </>
      )}

      <div className="chart-placeholder" aria-hidden="true">
        Chart will go here
      </div>

      <p className="nav-links">
        <Link href="/">← Back to file list</Link>
        <span className="nav-links-sep"></span>
        <DeleteFileButton fileId={id} redirectPath="/" />
      </p>
    </>
  );
}
