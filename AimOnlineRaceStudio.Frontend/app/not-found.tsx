import Link from 'next/link';

export default function NotFound() {
  return (
    <div>
      <h2>Not found</h2>
      <p>The requested resource could not be found.</p>
      <p className="nav-links">
        <Link href="/">← Back to file list</Link>
      </p>
    </div>
  );
}
