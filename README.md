# AIM Online Race Studio

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

Output is in `XrkApi/bin/Release/net9.0/win-x64/publish/`. Copy that folder to the Windows machine and run `XrkApi.exe`. The .NET runtime is included, so the target machine does not need it installed.

For a smaller package when .NET 9 is already on the target machine, use `--self-contained false` instead.

### Run locally (on Windows)

```bash
cd XrkApi
dotnet run
```

The service listens on **port 5000** by default (see `Properties/launchSettings.json`). Only **localhost and private network** IPs (e.g. 10.x, 192.168.x, 172.16–31.x) are allowed; public internet IPs receive 403. Example: `http://localhost:5000/health` or `http://10.0.0.1:5000/health` from another machine on the same LAN.

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
