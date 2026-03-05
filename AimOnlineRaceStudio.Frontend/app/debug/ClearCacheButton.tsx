'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { clearXrkApiCache } from '@/lib/api';

export function ClearCacheButton() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  async function handleClear() {
    setLoading(true);
    setMessage(null);
    try {
      const result = await clearXrkApiCache();
      setMessage({ ok: true, text: `Cleared ${result.cleared} cache entries.` });
      router.refresh();
    } catch (e) {
      setMessage({
        ok: false,
        text: e instanceof Error ? e.message : 'Failed to clear cache',
      });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="cache-actions">
      <button
        type="button"
        onClick={handleClear}
        disabled={loading}
        className="btn btn-primary"
        aria-busy={loading}
      >
        {loading ? 'Clearing…' : 'Clear all cache'}
      </button>
      {message && (
        <p className={message.ok ? 'status-ok' : 'status-fail'} role="status">
          {message.text}
        </p>
      )}
    </div>
  );
}
