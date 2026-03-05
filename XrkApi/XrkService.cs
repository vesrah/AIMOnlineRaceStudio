using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace XrkApi;

public static class XrkService
{
    private const string OpenFileError = "The DLL could not open or parse the XRK file. Ensure it is a valid 64-bit DLL and 64-bit runtime.";
    public const string PrivateAccessOnlyMessage = "Forbidden: only local and private network access is allowed.";

    private static readonly object CacheLock = new();

    private static bool IsValidCacheKey(string key) =>
        key.Length == 64 && key.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static string GetCacheDir()
    {
        var dir = Environment.GetEnvironmentVariable("XRK_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(dir)) return dir.Trim();
        return Path.Combine(Path.GetTempPath(), "XrkApi", "cache");
    }

    private static void EnsureCacheDir()
    {
        var dir = GetCacheDir();
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private sealed class CachedExport
    {
        public required string MetadataJson { get; init; }
        public required string Csv { get; init; }
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static CachedExport? GetFromCache(string key)
    {
        if (!IsValidCacheKey(key)) return null;
        lock (CacheLock)
        {
            var dir = GetCacheDir();
            var metaPath = Path.Combine(dir, $"{key}.metadata.json");
            var csvPath = Path.Combine(dir, $"{key}.csv");
            if (!File.Exists(metaPath) || !File.Exists(csvPath)) return null;
            try
            {
                var metadataJson = File.ReadAllText(metaPath);
                var csv = File.ReadAllText(csvPath);
                return new CachedExport { MetadataJson = metadataJson, Csv = csv };
            }
            catch { return null; }
        }
    }

    public static string GetCacheDirectory() => GetCacheDir();

    public static int GetCacheEntryCount()
    {
        lock (CacheLock)
        {
            var dir = GetCacheDir();
            if (!Directory.Exists(dir)) return 0;
            try
            {
                return Directory.GetFiles(dir, "*.metadata.json").Length;
            }
            catch { return 0; }
        }
    }

    public static bool CacheExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !IsValidCacheKey(key)) return false;
        lock (CacheLock)
        {
            var metaPath = Path.Combine(GetCacheDir(), $"{key}.metadata.json");
            return File.Exists(metaPath);
        }
    }

    private static void AddToCache(string key, object metadata, string csv)
    {
        if (!IsValidCacheKey(key)) return;
        lock (CacheLock)
        {
            EnsureCacheDir();
            var dir = GetCacheDir();
            var metaPath = Path.Combine(dir, $"{key}.metadata.json");
            var csvPath = Path.Combine(dir, $"{key}.csv");
            var metaTemp = Path.Combine(dir, $"{key}.metadata.json.{Guid.NewGuid():N}.tmp");
            var csvTemp = Path.Combine(dir, $"{key}.csv.{Guid.NewGuid():N}.tmp");
            try
            {
                var metadataJson = JsonSerializer.Serialize(metadata);
                File.WriteAllText(metaTemp, metadataJson);
                File.Move(metaTemp, metaPath, overwrite: true);
                File.WriteAllText(csvTemp, csv);
                File.Move(csvTemp, csvPath, overwrite: true);
            }
            catch
            {
                try { if (File.Exists(metaTemp)) File.Delete(metaTemp); } catch { }
                try { if (File.Exists(csvTemp)) File.Delete(csvTemp); } catch { }
            }
        }
    }

    private static readonly IPNetwork[] PrivateRanges =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("169.254.0.0/16")
    ];

    public static bool IsLocalOrPrivateAddress(IPAddress? address)
    {
        if (address is null) return false;
        if (IPAddress.IsLoopback(address)) return true;
        if (address.GetAddressBytes().Length != 4) return false;
        foreach (var network in PrivateRanges)
            if (network.Contains(address)) return true;
        return false;
    }

    public static async Task<IResult> WithXrkFileAsync(IFormFile file, bool returnCsv, bool useCache = true)
    {
        if (file.Length == 0)
            return Results.BadRequest("No file uploaded.");

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xrk");
        var fileName = file.FileName;

        try
        {
            await using (var stream = new FileStream(tempFilePath, FileMode.Create))
                await file.CopyToAsync(stream);

            string? hash = null;
            if (useCache)
            {
                hash = ComputeFileHash(tempFilePath);
                var cached = GetFromCache(hash);
                if (cached is not null)
                {
                    if (returnCsv)
                        return Results.File(Encoding.UTF8.GetBytes(cached.Csv), "text/csv", Path.ChangeExtension(fileName, ".csv"));
                    return Results.Content(cached.MetadataJson, "application/json");
                }
            }

            var result = await Task.Run(() =>
            {
                try
                {
                    using var sdk = new MatLabXrkWrapper();
                    if (sdk.Open(tempFilePath) <= 0)
                    {
                        var detail = MatLabXrkWrapper.GetLastOpenError();
                        var msg = string.IsNullOrWhiteSpace(detail) ? OpenFileError : $"{OpenFileError} {detail}";
                        return (Success: false, Message: msg, ClientError: true, Metadata: (object?)null, Csv: (string?)null);
                    }
                    var metadata = BuildMetadata(sdk);
                    var csv = BuildCsv(sdk);
                    return (Success: true, Message: (string?)null, ClientError: false, Metadata: metadata, Csv: csv);
                }
                catch (Exception ex)
                {
                    return (Success: false, Message: $"The file could not be read or parsed: {ex.Message}", ClientError: false, Metadata: (object?)null, Csv: (string?)null);
                }
            });

            if (!result.Success)
                return result.ClientError ? Results.BadRequest(result.Message!) : Results.Problem(result.Message!, statusCode: 500);

            if (useCache && hash is not null)
                AddToCache(hash, result.Metadata!, result.Csv!);

            if (returnCsv)
                return Results.File(Encoding.UTF8.GetBytes(result.Csv!), "text/csv", Path.ChangeExtension(fileName, ".csv"));
            return Results.Json(result.Metadata);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Internal Error: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
        }
    }

    public static string BuildCsv(IXrkFileReader sdk)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Time,Value,Channel,Units,Vehicle,Track");

        var vehicle = sdk.GetVehicleName();
        var track = sdk.GetTrackName();
        var channelNames = sdk.GetChannelNames();

        for (var c = 0; c < channelNames.Count; c++)
        {
            var channelName = channelNames[c];
            var channelUnits = sdk.GetChannelUnits(c);
            var (times, values) = sdk.GetChannelData(c);

            var rowCount = Math.Min(times.Length, values.Length);
            for (var i = 0; i < rowCount; i++)
                csv.AppendLine($"{times[i]},{values[i]},{channelName},{channelUnits},{vehicle},{track}");
        }

        return csv.ToString();
    }

    public static object BuildMetadata(IXrkFileReader sdk)
    {
        var channelNames = sdk.GetChannelNames();
        var channels = new List<object>(channelNames.Count);
        for (var c = 0; c < channelNames.Count; c++)
        {
            channels.Add(new
            {
                Index = c,
                Name = channelNames[c],
                Units = sdk.GetChannelUnits(c)
            });
        }

        return new
        {
            sdk.LibraryDate,
            sdk.LibraryTime,
            Vehicle = sdk.GetVehicleName(),
            Track = sdk.GetTrackName(),
            Racer = sdk.GetRacerName(),
            LapCount = sdk.LapCount,
            Laps = sdk.GetLaps(),
            Channels = channels,
            GpsChannels = sdk.GetGpsChannelNames()
        };
    }

    public static IResult GetHealthResult(IHostEnvironment env)
    {
        try
        {
            using var sdk = new MatLabXrkWrapper();
            return Results.Ok(new
            {
                Status = "Healthy",
                Service = "XRK-Convert",
                Environment = env.EnvironmentName,
                DllLoaded = true,
                DllLibraryDate = sdk.LibraryDate,
                DllLibraryTime = sdk.LibraryTime,
                Runtime = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                MaxRequestBodySizeMb = 100,
                CacheDirectory = GetCacheDirectory(),
                CachedExportCount = GetCacheEntryCount()
            });
        }
        catch (Exception ex)
        {
            return Results.Json(new
            {
                Status = "Unhealthy",
                Service = "XRK-Convert",
                Error = ex.Message,
                Type = ex.GetType().Name,
                Runtime = RuntimeInformation.OSDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            }, statusCode: 500);
        }
    }
}
