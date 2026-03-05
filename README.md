# AIM Online Race Studio

**Development host:** This repo is set up to run on an **M2 Max MacBook Pro** (macOS, Apple Silicon). Docker (postgres, backend, frontend) runs natively on the Mac. The **XrkApi** is Windows-only and runs on a separate machine (see below).

---

## XrkApi — XRK to CSV and metadata microservice

This project is a small Windows-only API that converts AIM XRK data files to CSV and metadata using the MatLab XRK native DLL. It is intended to run as a microservice so the rest of the AIM Online Race Studio stack can stay off Windows.

### Requirements

- .NET 9 SDK
- Windows (x64) to *run* the service — the MatLab XRK DLL is Windows-only
- `XrkApi/External/MatLabXRK-2022-64-ReleaseU.dll` must be present (in this repo for publish to include it)

### Build on Mac, deploy to Windows

This project can be built on a Mac and the output copied to a Windows machine. The native DLL is just a file; it is included in the publish folder and only runs on Windows.

```bash
cd XrkApi
dotnet publish -c Release -r win-x64 --self-contained true
```

**Deployable output:** `XrkApi/bin/Release/net9.0/win-x64/publish/`. Copy that folder to the Windows machine and run `XrkApi.exe`. The .NET runtime is included, so the target machine does not need it installed.

For a smaller package when .NET 9 is already on the target machine, use `--self-contained false` instead.

**Why multiple folders under `bin/Release/net9.0/`:** A normal `dotnet build -c Release` (no RID) writes to `net9.0/`. Running `dotnet publish -r win-x64` adds `net9.0/win-x64/` (RID build) and `net9.0/win-x64/publish/` (the folder to deploy). So you can end up with three copies. Run `dotnet clean` then only the publish command above if you want to avoid leftover output; `bin/` is gitignored.

### Run locally (on Windows)

```bash
cd XrkApi
dotnet run
```

The service listens on **port 5000** by default (see `Properties/launchSettings.json`). Only **localhost and private network** IPs (e.g. 10.x, 192.168.x, 172.16–31.x) are allowed; public internet IPs receive 403. Example: `http://localhost:5000/health` or `http://10.0.0.1:5000/health` from another machine on the same LAN.

**Optional shared token:** Set the environment variable **`XRK_API_SHARED_TOKEN`** (or `XrkApi:SharedToken` in appsettings) to a secret. When set, every request must include `Authorization: Bearer <token>`; otherwise XrkApi returns 401. Use the same value as **`XrkApi__SharedToken`** on the backend so the backend can call XrkApi.

### API

| Method | Path       | Description |
|--------|------------|-------------|
| POST   | `/csv` | Upload an XRK file; returns a CSV with Time, Value, Channel, Units, Vehicle, Track. |
| POST   | `/metadata` | Upload an XRK file; returns JSON with vehicle, track, racer, laps, channel list, GPS channels. |
| GET    | `/cache/{key}` | Check whether a cache key exists. Returns `{ "key": "...", "exists": true/false }`. |
| GET    | `/health` | Service and DLL health check. |

**CSV:** `POST /csv` — send the XRK file as form data (e.g. `multipart/form-data` with a file field). Response is `text/csv` with a suggested filename. Optional query: `?nocache=true` to skip cache.

**Metadata:** Same upload; response is JSON (library info, vehicle, track, racer, lap count, laps, channels with index/name/units, GPS channel names). Optional query: `?nocache=true` to skip cache.

#### Cache key (for `/cache/{key}` and consistent hashing)

Exports are cached by file content. The cache key is the **SHA-256 hash of the raw file bytes**, encoded as a **hexadecimal string** (uppercase A–F, 64 characters). Consumers of this API should build the same key to check cache before uploading:

1. Read the entire XRK file into a byte array (or stream).
2. Compute SHA-256 over those bytes (no encoding or transformation).
3. Encode the 32-byte hash as 64 hex characters (e.g. `Convert.ToHexString(hash)` in .NET, or equivalent).

Example (C#): `Convert.ToHexString(SHA256.HashData(fileBytes))`. The key is case-sensitive; the service uses uppercase hex.

#### Disk cache

Exports are stored on disk (not in memory). Each cache entry is two files: `{key}.metadata.json` and `{key}.csv`. The cache directory is created automatically.

- **Location:** Set the **`XRK_CACHE_DIR`** environment variable to a full path for the cache directory. If unset, the default is `%TEMP%\XrkApi\cache` (e.g. `C:\Users\...\AppData\Local\Temp\XrkApi\cache` on Windows).
- **Persistence:** Cache survives process restarts. Clear it by deleting the contents of the cache directory.

### Tests

```bash
dotnet test XrkApi.Tests/XrkApi.Tests.csproj
```

The test project uses a fake XRK reader (no DLL or real files). Requires .NET 9 SDK.

---

## Backend + Postgres (Workstream A)

A cross-platform .NET 9 Web API in `AimOnlineRaceStudio.Api/` stores converted XRK metadata and CSV in Postgres, checks by file hash before calling XrkApi, and exposes REST endpoints for the frontend.

### Running with Docker (postgres + backend + frontend) on the Mac

From the repo root on your **Mac**:

```bash
docker compose up --build
```

Docker Desktop on Apple Silicon builds and runs Linux ARM64 images; postgres, backend, and frontend all run in containers on the Mac.

- **Postgres** runs on port 5432 (user `aim`, password `aim`, database `aimrace`). Schema is applied automatically when the backend starts.
- **Backend** runs on port **5001** (e.g. `http://localhost:5001`). Endpoints: `GET /api/files`, `GET /api/files/:id`, `GET /api/files/:id/csv`, `POST /api/files/upload`.
- **Frontend** runs on port **3000**. Open **http://localhost:3000** in your browser to list files, upload XRK, and view file metadata.

**XrkApi (Windows-only)** must run on a **separate Windows machine** (e.g. at `http://10.0.0.44:5000` on your LAN). The backend in Docker forwards uploads to that URL. If you run XrkApi in a **Windows VM on the same Mac**, set `XrkApi__BaseUrl=http://host.docker.internal:5000` so the backend container can reach it. Override the URL in `docker-compose.yml` or via env.

**Optional shared token:** To require a secret for backend–XrkApi calls, set the same value on both sides. On the **XrkApi** (Windows) host: `XRK_API_SHARED_TOKEN=your-secret`. On the **backend** (env or docker-compose): `XrkApi__SharedToken=your-secret`. The backend then sends `Authorization: Bearer your-secret`; XrkApi rejects requests without a valid token (401). If the token is not set on either side, no auth is applied.

CSV files are stored in a Docker volume (`csvdata`) under `/data/csv` in the backend container; Postgres stores only the storage key. No 120MB CSV is buffered in memory—streaming from XrkApi to disk and from disk to response.

**Hot reload (dev):** `docker-compose.override.yml` is merged automatically when you run `docker compose up`. It mounts API and Frontend source and runs `dotnet watch run` and `npm run dev`, so code changes are picked up without rebuilding. For a production-style build with no mounts, run `docker compose -f docker-compose.yml up --build` (omit the override).

### Run backend locally on the Mac (without Docker)

1. Start Postgres (e.g. `docker run -e POSTGRES_USER=aim -e POSTGRES_PASSWORD=aim -e POSTGRES_DB=aimrace -p 5432:5432 postgres:16-alpine`).
2. Set `ConnectionStrings__Default` and optionally `XrkApi__BaseUrl` (default `http://10.0.0.44:5000`) and `CsvStorage__VolumePath` (default `/data/csv`; use a local path like `./data/csv`).
3. Run: `cd AimOnlineRaceStudio.Api && dotnet run`. The API listens on the port in `launchSettings.json` or 5000/5001. XrkApi must still run on Windows (or a Windows VM) and be reachable at the URL you set.
