import type { Metadata } from 'next';
import '@/styles/globals.scss';

export const metadata: Metadata = {
  title: 'AIM Online Race Studio',
  description: 'View and analyze XRK telemetry files',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body>
        <div className="container">
          <header className="page-header">
            <h1>AIM Online Race Studio</h1>
            <nav className="nav-links">
              <a href="/">Files</a>
              <a href="/upload">Upload XRK</a>
              <a href="/debug">Debug</a>
            </nav>
          </header>
          <main>{children}</main>
        </div>
      </body>
    </html>
  );
}
