import Link from 'next/link';
import { notFound } from 'next/navigation';
import { getFile } from '@/lib/api';
import { formatLapTime } from '@/lib/format';

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
        <dt>Lap count</dt>
        <dd>{detail.lap_count}</dd>
        <dt>Created</dt>
        <dd>{formatDate(detail.created_at)}</dd>
      </dl>

      {detail.laps.length > 0 && (
        <>
          <h3>Laps</h3>
          <ul>
            {detail.laps.map((lap) => (
              <li key={lap.lap_index}>
                Lap {lap.lap_index}: start {formatLapTime(lap.start_sec)}, duration {formatLapTime(lap.duration_sec)}
              </li>
            ))}
          </ul>
        </>
      )}

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
      </p>
    </>
  );
}
