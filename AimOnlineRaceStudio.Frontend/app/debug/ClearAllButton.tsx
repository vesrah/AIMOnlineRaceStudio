'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { clearAllData } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils';

export function ClearAllButton() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  async function handleClear() {
    if (!confirm('Delete all files and data from the database and disk? This cannot be undone.'))
      return;
    setLoading(true);
    setMessage(null);
    try {
      const result = await clearAllData();
      setMessage({ ok: true, text: `Cleared ${result.deleted} file(s). You can re-import from the upload page.` });
      router.refresh();
    } catch (e) {
      setMessage({
        ok: false,
        text: getErrorMessage(e, 'Failed to clear data'),
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
        className="btn btn-danger"
        aria-busy={loading}
      >
        {loading ? 'Clearing…' : 'Clear all data'}
      </button>
      {message && (
        <p className={message.ok ? 'status-ok' : 'status-fail'} role="status">
          {message.text}
        </p>
      )}
    </div>
  );
}
