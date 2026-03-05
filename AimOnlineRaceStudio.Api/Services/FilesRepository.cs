using Npgsql;
using AimOnlineRaceStudio.Api.Models;

namespace AimOnlineRaceStudio.Api.Services;

public interface IFilesRepository
{
    Task<Guid?> GetFileIdByHashAsync(string fileHash, CancellationToken ct = default);
    Task<FileRecord?> GetFileByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<FileListItem>> ListFilesAsync(CancellationToken ct = default);
    Task<Dictionary<Guid, double>> GetShortestMiddleLapByFileIdsAsync(IReadOnlyList<Guid> fileIds, CancellationToken ct = default);
    Task<FileWithDetails?> GetFileWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<string?> GetCsvStorageKeyAsync(Guid fileId, CancellationToken ct = default);
    Task<(Guid Id, bool Conflict)> InsertFileAsync(FileRecord file, XrkMetadataDto metadata, string csvStorageKey, long? csvByteSize, CancellationToken ct = default);
    Task<(bool Deleted, string? StorageKey)> DeleteFileAsync(Guid id, CancellationToken ct = default);
    Task<(long TotalBytes, int FileCount)> GetStorageStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ClearAllFilesAsync(CancellationToken ct = default);
}

public record FileRecord(
    Guid Id,
    string FileHash,
    string? Filename,
    string? LibraryDate,
    string? LibraryTime,
    string? Vehicle,
    string? Track,
    string? Racer,
    long? LoggerId,
    int LapCount,
    DateTime CreatedAt,
    DateTime DateCreated,
    DateTime LastModified);

public record FileListItem(
    Guid Id,
    string FileHash,
    string? Filename,
    string? Vehicle,
    string? Track,
    DateTime CreatedAt,
    string? LibraryDate,
    string? LibraryTime,
    long? LoggerId,
    int LapCount,
    DateTime DateCreated,
    DateTime LastModified);

public record FileWithDetails(
    FileRecord File,
    List<LapDto> Laps,
    List<ChannelDto> Channels,
    List<string> GpsChannels);

public class FilesRepository : IFilesRepository
{
    private readonly IConfiguration _config;

    public FilesRepository(IConfiguration config)
    {
        _config = config;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_config.GetConnectionString("Default"));
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<Guid?> GetFileIdByHashAsync(string fileHash, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT id FROM xrk_files WHERE file_hash = @h", conn);
        cmd.Parameters.AddWithValue("h", fileHash);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : (Guid?)null;
    }

    public async Task<FileRecord?> GetFileByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, file_hash, filename, library_date, library_time, vehicle, track, racer, logger_id, lap_count, created_at, date_created, last_modified FROM xrk_files WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new FileRecord(
            r.GetGuid(0),
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.IsDBNull(3) ? null : r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : r.GetString(6),
            r.IsDBNull(7) ? null : r.GetString(7),
            r.IsDBNull(8) ? null : r.GetInt64(8),
            r.GetInt32(9),
            r.GetDateTime(10),
            r.GetDateTime(11),
            r.GetDateTime(12));
    }

    public async Task<List<FileListItem>> ListFilesAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, file_hash, filename, vehicle, track, created_at, library_date, library_time, logger_id, lap_count, date_created, last_modified FROM xrk_files ORDER BY created_at DESC", conn);
        var list = new List<FileListItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new FileListItem(
                r.GetGuid(0),
                r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetDateTime(5),
                r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.IsDBNull(8) ? null : r.GetInt64(8),
                r.GetInt32(9),
                r.GetDateTime(10),
                r.GetDateTime(11)));
        }
        return list;
    }

    /// <summary>
    /// For each file, the minimum duration_sec among laps that are not the first or last lap (by lap_index).
    /// Files with 0, 1, or 2 laps have no "middle" lap and are omitted from the result.
    /// </summary>
    public async Task<Dictionary<Guid, double>> GetShortestMiddleLapByFileIdsAsync(IReadOnlyList<Guid> fileIds, CancellationToken ct = default)
    {
        if (fileIds.Count == 0) return new Dictionary<Guid, double>();
        await using var conn = await OpenAsync(ct);
        var result = new Dictionary<Guid, double>();
        await using var cmd = new NpgsqlCommand(
            @"WITH bounds AS (
                SELECT file_id, MIN(lap_index) AS first_idx, MAX(lap_index) AS last_idx
                FROM xrk_laps WHERE file_id = ANY(@ids) GROUP BY file_id
              ),
              middle_laps AS (
                SELECT l.file_id, l.duration_sec
                FROM xrk_laps l
                JOIN bounds b ON b.file_id = l.file_id AND l.lap_index > b.first_idx AND l.lap_index < b.last_idx
              )
              SELECT file_id, MIN(duration_sec) AS shortest_sec FROM middle_laps GROUP BY file_id", conn);
        cmd.Parameters.AddWithValue("ids", fileIds.ToArray());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result[r.GetGuid(0)] = r.GetDouble(1);
        return result;
    }

    public async Task<FileWithDetails?> GetFileWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        var file = await GetFileByIdAsync(id, ct);
        if (file == null) return null;

        await using var conn = await OpenAsync(ct);
        var laps = new List<LapDto>();
        await using (var cmd = new NpgsqlCommand("SELECT lap_index, start_sec, duration_sec FROM xrk_laps WHERE file_id = @id ORDER BY lap_index", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                laps.Add(new LapDto { Index = r.GetInt32(0), Start = r.GetDouble(1), Duration = r.GetDouble(2) });
        }

        var channels = new List<ChannelDto>();
        await using (var cmd = new NpgsqlCommand("SELECT channel_index, name, units FROM xrk_channels WHERE file_id = @id ORDER BY channel_index", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                channels.Add(new ChannelDto { Index = r.GetInt32(0), Name = r.GetString(1), Units = r.IsDBNull(2) ? null : r.GetString(2) });
        }

        var gpsChannels = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT name FROM xrk_gps_channels WHERE file_id = @id ORDER BY name", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                gpsChannels.Add(r.GetString(0));
        }

        return new FileWithDetails(file, laps, channels, gpsChannels);
    }

    public async Task<string?> GetCsvStorageKeyAsync(Guid fileId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT storage_key FROM xrk_csv WHERE file_id = @id", conn);
        cmd.Parameters.AddWithValue("id", fileId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task<(bool Deleted, string? StorageKey)> DeleteFileAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        string? storageKey = null;
        await using (var cmd = new NpgsqlCommand("SELECT storage_key FROM xrk_csv WHERE file_id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync(ct);
            storageKey = result as string;
        }
        await using (var cmd = new NpgsqlCommand("DELETE FROM xrk_files WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            return (rows > 0, storageKey);
        }
    }

    public async Task<(long TotalBytes, int FileCount)> GetStorageStatsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(SUM(byte_size), 0)::BIGINT, COUNT(*)::INT FROM xrk_csv", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            return (0, 0);
        var totalBytes = r.GetInt64(0);
        var fileCount = r.GetInt32(1);
        return (totalBytes, fileCount);
    }

    public async Task<IReadOnlyList<string>> ClearAllFilesAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        var keys = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT storage_key FROM xrk_csv", conn))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
                keys.Add(r.GetString(0));
        }
        await using (var cmd = new NpgsqlCommand("TRUNCATE TABLE xrk_files CASCADE", conn))
            await cmd.ExecuteNonQueryAsync(ct);
        return keys;
    }

    private static async Task<Guid?> GetFileIdByHashConflictAsync(NpgsqlConnection conn, string fileHash, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT id FROM xrk_files WHERE file_hash = @h", conn);
        cmd.Parameters.AddWithValue("h", fileHash);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    public async Task<(Guid Id, bool Conflict)> InsertFileAsync(FileRecord file, XrkMetadataDto metadata, string csvStorageKey, long? csvByteSize, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var now = file.CreatedAt;
            await using (var cmd = new NpgsqlCommand(
                @"INSERT INTO xrk_files (id, file_hash, filename, library_date, library_time, vehicle, track, racer, logger_id, lap_count, date_created, last_modified, created_at)
                  VALUES (@id, @fh, @fn, @ld, @lt, @v, @t, @r, @lid, @lc, @dc, @lm, @ca)
                  ON CONFLICT (file_hash) DO NOTHING
                  RETURNING id", conn))
            {
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("id", file.Id);
                cmd.Parameters.AddWithValue("fh", file.FileHash);
                cmd.Parameters.AddWithValue("fn", (object?)file.Filename ?? DBNull.Value);
                cmd.Parameters.AddWithValue("ld", (object?)file.LibraryDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("lt", (object?)file.LibraryTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("v", (object?)file.Vehicle ?? DBNull.Value);
                cmd.Parameters.AddWithValue("t", (object?)file.Track ?? DBNull.Value);
                cmd.Parameters.AddWithValue("r", (object?)file.Racer ?? DBNull.Value);
                cmd.Parameters.AddWithValue("lid", (object?)file.LoggerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("lc", file.LapCount);
                cmd.Parameters.AddWithValue("dc", now);
                cmd.Parameters.AddWithValue("lm", now);
                cmd.Parameters.AddWithValue("ca", now);
                var insertedId = await cmd.ExecuteScalarAsync(ct);
                if (insertedId == null || insertedId is DBNull)
                {
                    await tx.RollbackAsync(ct);
                    var existingId = await GetFileIdByHashConflictAsync(conn, file.FileHash, ct);
                    return (existingId ?? file.Id, Conflict: true);
                }
            }

            foreach (var lap in metadata.Laps ?? [])
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO xrk_laps (file_id, lap_index, start_sec, duration_sec) VALUES (@fid, @idx, @start, @dur)", conn);
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("fid", file.Id);
                cmd.Parameters.AddWithValue("idx", lap.Index);
                cmd.Parameters.AddWithValue("start", lap.Start);
                cmd.Parameters.AddWithValue("dur", lap.Duration);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var ch in metadata.Channels ?? [])
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO xrk_channels (file_id, channel_index, name, units) VALUES (@fid, @idx, @name, @units)", conn);
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("fid", file.Id);
                cmd.Parameters.AddWithValue("idx", ch.Index);
                cmd.Parameters.AddWithValue("name", ch.Name ?? "");
                cmd.Parameters.AddWithValue("units", (object?)ch.Units ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var name in metadata.GpsChannels ?? [])
            {
                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO xrk_gps_channels (file_id, name) VALUES (@fid, @name)", conn);
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("fid", file.Id);
                cmd.Parameters.AddWithValue("name", name);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var cmd = new NpgsqlCommand(
                "INSERT INTO xrk_csv (file_id, storage_key, content_type, byte_size) VALUES (@fid, @key, 'text/csv', @size)", conn))
            {
                cmd.Transaction = tx;
                cmd.Parameters.AddWithValue("fid", file.Id);
                cmd.Parameters.AddWithValue("key", csvStorageKey);
                cmd.Parameters.AddWithValue("size", (object?)csvByteSize ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return (file.Id, Conflict: false);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

}
