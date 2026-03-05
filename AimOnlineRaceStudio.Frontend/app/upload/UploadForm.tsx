'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { uploadFile } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils';
import type { UploadSuccessResponse } from '@/types/api';

type FileResult = { name: string; ok: true; data: UploadSuccessResponse } | { name: string; ok: false; error: string };

export default function UploadForm() {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<FileResult[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  function onFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const list = e.target.files;
    setFiles(list ? Array.from(list) : []);
    setError(null);
    setResults(null);
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (files.length === 0) {
      setError('Please select one or more XRK files.');
      return;
    }
    setError(null);
    setResults([]);
    setLoading(true);
    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      try {
        const data = await uploadFile(file);
        setResults((prev) => [...(prev ?? []), { name: file.name, ok: true as const, data }]);
      } catch (err) {
        setResults((prev) => [
          ...(prev ?? []),
          { name: file.name, ok: false as const, error: getErrorMessage(err, 'Upload failed') },
        ]);
      }
    }
    setLoading(false);
    setFiles([]);
    if (fileInputRef.current) fileInputRef.current.value = '';
  }

  const succeeded = results?.filter((r) => r.ok) ?? [];
  const failed = results?.filter((r) => !r.ok) ?? [];

  return (
    <>
      <section className="page-header">
        <h2>Upload XRK files</h2>
        <p className="muted">Select one or more .xrk files to convert and store.</p>
      </section>

      <form onSubmit={handleSubmit} className="form-group">
        <div className="form-group">
          <label htmlFor="xrk-file">XRK file(s)</label>
          <input
            ref={fileInputRef}
            id="xrk-file"
            type="file"
            accept=".xrk,.XRK"
            multiple
            onChange={onFileChange}
            disabled={loading}
          />
          {files.length > 0 && (
            <p className="muted">{files.length} file(s) selected</p>
          )}
        </div>
        <button type="submit" className="btn" disabled={loading || files.length === 0}>
          {loading
            ? `Uploading ${results?.length ?? 0} of ${files.length}…`
            : files.length > 1
              ? `Upload ${files.length} files`
              : 'Upload'}
        </button>
      </form>

      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}

      {results && results.length > 0 && (
        <div className="upload-results" role="status" aria-live="polite">
          <p>
            <strong>{succeeded.length} succeeded</strong>
            {failed.length > 0 && `, ${failed.length} failed`}
            {loading && ` · Uploading ${results.length} of ${files.length}…`}
          </p>
          {succeeded.length > 0 && (
            <ul className="upload-result-list">
              {succeeded.map((r) =>
                r.ok ? (
                  <li key={r.data.id}>
                    <code>{r.name}</code>
                    {' → '}
                    <Link href={`/files/${r.data.id}`}>View details</Link>
                  </li>
                ) : null
              )}
            </ul>
          )}
          {failed.length > 0 && (
            <ul className="upload-result-list upload-result-errors">
              {failed.map((r) =>
                !r.ok ? (
                  <li key={r.name}>
                    <code>{r.name}</code>: {r.error}
                  </li>
                ) : null
              )}
            </ul>
          )}
        </div>
      )}

      <p className="nav-links">
        <Link href="/">← Back to file list</Link>
      </p>
    </>
  );
}
