/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'standalone',
  experimental: {
    optimizePackageImports: ['react', 'react-dom'],
  },
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

// Only load bundle-analyzer when ANALYZE=true (e.g. npm run analyze). Avoids requiring the dev dep in Docker.
if (process.env.ANALYZE === 'true') {
  const withBundleAnalyzer = require('@next/bundle-analyzer')({ enabled: true });
  module.exports = withBundleAnalyzer(nextConfig);
} else {
  module.exports = nextConfig;
}
