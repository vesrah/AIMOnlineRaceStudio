using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Models;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Services;

public interface IFilesService
{
    Task<(Guid FileId, bool Existing)> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public class FilesService : IFilesService
{
    private readonly IFilesRepository _repo;
    private readonly IXrkApiClient _xrkApi;
    private readonly IOptions<CsvStorageOptions> _csvOptions;
    private readonly ILogger<FilesService> _logger;

    public FilesService(
        IFilesRepository repo,
        IXrkApiClient xrkApi,
        IOptions<CsvStorageOptions> csvOptions,
        ILogger<FilesService> logger)
    {
        _repo = repo;
        _xrkApi = xrkApi;
        _csvOptions = csvOptions;
        _logger = logger;
    }

    public async Task<(Guid FileId, bool Existing)> UploadAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xrk");
        try
        {
            await using (var fs = File.Create(tempPath))
                await fileStream.CopyToAsync(fs, ct);

            await using var hashStream = File.OpenRead(tempPath);
            var fileHash = await FileHashService.ComputeSha256UppercaseHexAsync(hashStream, ct);

            var existingId = await _repo.GetFileIdByHashAsync(fileHash, ct);
            if (existingId.HasValue)
            {
                _logger.LogInformation("File already exists: hash {Hash}, id {Id}", fileHash, existingId.Value);
                return (existingId.Value, true);
            }

            // Call XrkApi: metadata first, then CSV (stream to volume).
            await using var metaStream = File.OpenRead(tempPath);
            var metadata = await _xrkApi.GetMetadataAsync(metaStream, fileName, ct);
            if (metadata == null)
                throw new InvalidOperationException("XrkApi returned no metadata.");

            var volumePath = _csvOptions.Value.VolumePath?.Trim() ?? "/data/csv";
            var csvFileName = $"{fileHash}.csv";
            var csvPath = Path.Combine(volumePath, csvFileName);
            Directory.CreateDirectory(volumePath);
            await using var csvStream = File.OpenRead(tempPath);
            await _xrkApi.StreamCsvToFileAsync(csvStream, fileName, csvPath, ct);

            long? csvByteSize = null;
            try
            {
                var fi = new FileInfo(csvPath);
                if (fi.Exists) csvByteSize = fi.Length;
            }
            catch { /* ignore */ }

            var file = new FileRecord(
                Id: Guid.NewGuid(),
                FileHash: fileHash,
                Filename: fileName,
                LibraryDate: metadata.LibraryDate,
                LibraryTime: metadata.LibraryTime,
                Vehicle: metadata.Vehicle,
                Track: metadata.Track,
                Racer: metadata.Racer,
                LapCount: metadata.LapCount,
                CreatedAt: DateTime.UtcNow);

            var (insertedId, conflict) = await _repo.InsertFileAsync(file, metadata, csvFileName, csvByteSize, ct);
            if (conflict)
            {
                _logger.LogInformation("Concurrent upload detected: hash {Hash} already exists as {Id}", fileHash, insertedId);
                return (insertedId, true);
            }
            _logger.LogInformation("Inserted file id {Id}, hash {Hash}", insertedId, fileHash);
            return (insertedId, false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var (deleted, storageKey) = await _repo.DeleteFileAsync(id, ct);
        if (!deleted) return false;
        if (!string.IsNullOrEmpty(storageKey))
        {
            var volumePath = _csvOptions.Value.VolumePath?.Trim() ?? "/data/csv";
            var path = Path.Combine(volumePath, storageKey);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogInformation("Deleted CSV file {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete CSV file {Path}", path);
            }
            var cacheKey = storageKey.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? storageKey[..^4]
                : storageKey;
            if (cacheKey.Length == 64)
            {
                try
                {
                    var (ok, removed) = await _xrkApi.ClearCacheEntryAsync(cacheKey, ct);
                    if (ok && removed)
                        _logger.LogInformation("Cleared XrkApi cache for hash {Hash}", cacheKey);
                    else if (!ok)
                        _logger.LogWarning("XrkApi unavailable when clearing cache for {Hash}", cacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clear XrkApi cache for {Hash}", cacheKey);
                }
            }
        }
        return true;
    }
}
