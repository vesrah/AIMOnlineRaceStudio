/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  // In dev, proxy /api to backend to avoid CORS when frontend and backend run on different origins.
  // Set NEXT_PUBLIC_USE_API_PROXY=true to enable (e.g. in .env.local).
  async rewrites() {
    const useProxy = process.env.NEXT_PUBLIC_USE_API_PROXY === 'true';
    const apiUrl = process.env.NEXT_PUBLIC_API_URL;
    if (useProxy && apiUrl) {
      try {
        const base = new URL(apiUrl);
        const target = `${base.origin}/`;
        return [
          { source: '/api/:path*', destination: `${target}api/:path*` },
        ];
      } catch (_) {
        return [];
      }
    }
    return [];
  },
};

module.exports = nextConfig;
