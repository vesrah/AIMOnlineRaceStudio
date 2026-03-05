'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { deleteFile } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils';

interface DeleteFileButtonProps {
  fileId: string;
  label?: string;
  redirectPath?: string;
  className?: string;
}

export function DeleteFileButton({ fileId, label = 'Delete', redirectPath, className = 'btn btn-danger' }: DeleteFileButtonProps) {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    if (!confirm('Delete this file? This cannot be undone.')) return;
    setLoading(true);
    setError(null);
    try {
      await deleteFile(fileId);
      if (redirectPath) router.push(redirectPath);
      else router.refresh();
    } catch (e) {
      setError(getErrorMessage(e, 'Delete failed'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <span className="delete-file-wrap">
      <button
        type="button"
        onClick={handleClick}
        disabled={loading}
        className={className}
        aria-busy={loading}
      >
        {loading ? 'Deleting…' : label}
      </button>
      {error && <span className="delete-file-error" role="alert">{error}</span>}
    </span>
  );
}
