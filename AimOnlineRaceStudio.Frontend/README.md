# AIM Online Race Studio — Frontend

Next.js 14+ (App Router) frontend for listing XRK files, uploading, and viewing file metadata. Connects to the backend API for data.

## Stack

- **Next.js 14+** (App Router), **TypeScript**, **React 18**, **SCSS**
- Typed API client in `lib/api.ts` calling backend REST endpoints
- Optional htmx for form/partial updates (not required for initial use)

## Project structure

```
AimOnlineRaceStudio.Frontend/
├── app/
│   ├── layout.tsx          # Root layout, nav
│   ├── page.tsx             # Home: file list (SSR)
│   ├── upload/page.tsx      # Upload XRK form
│   ├── files/[id]/page.tsx  # File detail + metadata + chart placeholder
│   └── not-found.tsx
├── components/              # (optional React components)
├── lib/
│   └── api.ts               # Backend API client
├── types/
│   └── api.ts               # API types (file, lap, channel, etc.)
├── styles/
│   ├── globals.scss
│   └── _variables.scss
├── next.config.js           # standalone output, optional /api proxy
├── Dockerfile               # Multi-stage Next.js standalone
└── README.md
```

## Configuration

### Backend base URL

Set the backend API base URL (no trailing slash), e.g. `http://localhost:5001`:

- **`.env.local`** (create from `.env.example`):
  ```bash
  NEXT_PUBLIC_API_URL=http://localhost:5001
  ```

- **Docker:** pass at runtime, e.g.:
  ```bash
  docker run -e NEXT_PUBLIC_API_URL=http://localhost:5001 -p 3000:3000 aim-frontend
  ```
  Or in docker-compose: `NEXT_PUBLIC_API_URL: "http://backend:8080"` (for same-stack) or the URL the browser uses to reach the backend.

The API client uses `NEXT_PUBLIC_API_URL` for all requests (`GET /api/files`, `GET /api/files/:id`, `POST /api/files/upload`, `GET /api/files/:id/csv`).

### CORS and dev proxy

- **Backend must allow the frontend origin** when the browser calls the backend directly (e.g. frontend at `http://localhost:3000`, backend at `http://localhost:5001`). Configure CORS on the backend to allow `http://localhost:3000` (and your production frontend origin).

- **Optional — avoid CORS in dev:** Use Next.js rewrites to proxy `/api` to the backend so the browser only talks to the Next server:
  1. In `.env.local` set:
     ```bash
     NEXT_PUBLIC_API_URL=http://localhost:5001
     NEXT_PUBLIC_USE_API_PROXY=true
     ```
  2. With this, the frontend will request `http://localhost:3000/api/...` and Next.js rewrites those to `http://localhost:5001/api/...`. No CORS needed for dev.

## Run locally

```bash
cd AimOnlineRaceStudio.Frontend
cp .env.example .env.local
# Edit .env.local and set NEXT_PUBLIC_API_URL to your backend (e.g. http://localhost:5001)
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). List and file detail pages use SSR and call the backend from the server; upload uses client-side fetch.

## Build and run with Docker

```bash
cd AimOnlineRaceStudio.Frontend
docker build -t aim-frontend .
docker run -p 3000:3000 -e NEXT_PUBLIC_API_URL=http://localhost:5001 aim-frontend
```

For production, set `NEXT_PUBLIC_API_URL` to the URL the **browser** uses to reach the backend (e.g. same host with a reverse proxy, or the public backend URL if CORS is configured).

The Dockerfile uses a multi-stage build: `node:20-alpine` for `npm ci` and `next build`, then copies `.next/standalone` and `.next/static` and runs `node server.js` (Next.js standalone output).

## Testing

Unit tests use [Vitest](https://vitest.dev/) and cover `lib/format.ts`, `lib/utils.ts`, and `lib/grouping.ts`.

```bash
npm run test        # run once
npm run test:watch  # watch mode
```

## Bundle size

Production build keeps **First Load JS** per route around ~98 kB (shared runtime ~87 kB plus small page chunks). Client-only UI (delete button, upload form, clear-cache button) is loaded in separate chunks via `next/dynamic`. To inspect the bundle:

```bash
npm run analyze
```

This runs the build with `@next/bundle-analyzer` and opens a report of chunk sizes.
