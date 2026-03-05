'use client';

import { useState } from 'react';
import Link from 'next/link';
import { uploadFile } from '@/lib/api';
import type { UploadSuccessResponse } from '@/types/api';

export default function UploadPage() {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<UploadSuccessResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!file) {
      setError('Please select a file.');
      return;
    }
    setError(null);
    setResult(null);
    setLoading(true);
    try {
      const data = await uploadFile(file);
      setResult(data);
    } catch (e) {
      const err = e as { message?: string };
      setError(err?.message ?? 'Upload failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <section className="page-header">
        <h2>Upload XRK file</h2>
      </section>

      <form onSubmit={handleSubmit} className="form-group">
        <div className="form-group">
          <label htmlFor="xrk-file">XRK file</label>
          <input
            id="xrk-file"
            type="file"
            accept=".xrk,.XRK"
            onChange={(e) => {
              const f = e.target.files?.[0];
              setFile(f ?? null);
              setError(null);
              setResult(null);
            }}
          />
        </div>
        <button type="submit" className="btn" disabled={loading}>
          {loading ? 'Uploading…' : 'Upload'}
        </button>
      </form>

      {error && (
        <div className="alert alert-error" role="alert">
          {error}
        </div>
      )}

      {result && (
        <div className="alert alert-success" role="status">
          <p><strong>Upload successful.</strong></p>
          <p>File: <code>{file?.name ?? result.id}</code></p>
          <p>
            <Link href={`/files/${result.id}`}>View file details</Link>
          </p>
        </div>
      )}

      <p className="nav-links">
        <Link href="/">← Back to file list</Link>
      </p>
    </>
  );
}
