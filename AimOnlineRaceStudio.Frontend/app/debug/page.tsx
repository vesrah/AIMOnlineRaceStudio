import Link from 'next/link';
import { getXrkApiHealth } from '@/lib/api';
import { ClearCacheButton } from './ClearCacheButton';

export const dynamic = 'force-dynamic';

export default async function DebugPage() {
  let data;
  let fetchError: string | null = null;

  try {
    data = await getXrkApiHealth();
  } catch (e) {
    fetchError = e instanceof Error ? e.message : 'Failed to load debug info';
  }

  const cacheCount =
    data?.result?.body && typeof data.result.body.CachedExportCount === 'number'
      ? data.result.body.CachedExportCount
      : null;

  return (
    <>
      <section className="page-header">
        <h2>Debug — XrkApi health</h2>
        <p className="muted">
          Backend proxies <code>GET /health</code> from the configured XrkApi (Windows service).
        </p>
      </section>

      {fetchError && (
        <div className="alert alert-error" role="alert">
          {fetchError}
        </div>
      )}

      {data && (
        <div className="debug-section">
          <h3>Configuration</h3>
          <p>
            <strong>Configured URL:</strong>{' '}
            <code>{data.configuredUrl}</code>
          </p>

          <h3>Cache</h3>
          <p>
            <strong>Cached exports:</strong>{' '}
            {cacheCount != null ? cacheCount : '—'}
          </p>
          <ClearCacheButton />

          <h3>Reachability</h3>
          <p>
            <strong>Reachable:</strong>{' '}
            <span className={data.result.reachable ? 'status-ok' : 'status-fail'}>
              {data.result.reachable ? 'Yes' : 'No'}
            </span>
          </p>
          {data.result.statusCode != null && (
            <p>
              <strong>HTTP status:</strong> <code>{data.result.statusCode}</code>
              {data.result.ok != null && (
                <span className={data.result.ok ? ' status-ok' : ' status-fail'}>
                  {' '}({data.result.ok ? 'OK' : 'Error'})
                </span>
              )}
            </p>
          )}
          {data.result.error && (
            <div className="alert alert-error">
              <strong>Error:</strong> {data.result.error}
            </div>
          )}

          {data.result.body && Object.keys(data.result.body).length > 0 && (
            <>
              <h3>XrkApi health response</h3>
              <pre className="debug-json">
                {JSON.stringify(data.result.body, null, 2)}
              </pre>
            </>
          )}
        </div>
      )}

      <p className="nav-back">
        <Link href="/">← Back to files</Link>
      </p>
    </>
  );
}
