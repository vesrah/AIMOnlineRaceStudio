using System.Text.Json.Serialization;
using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Models;
using AimOnlineRaceStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IFilesService _filesService;
    private readonly IFilesRepository _repo;
    private readonly IOptions<CsvStorageOptions> _csvOptions;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IFilesService filesService,
        IFilesRepository repo,
        IOptions<CsvStorageOptions> csvOptions,
        ILogger<FilesController> logger)
    {
        _filesService = filesService;
        _repo = repo;
        _csvOptions = csvOptions;
        _logger = logger;
    }

    /// <summary>
    /// Upload XRK file: compute hash, return existing file id if present, else convert via XrkApi and store.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            await using var stream = file.OpenReadStream();
            var (fileId, existing) = await _filesService.UploadAsync(stream, file.FileName, ct);
            if (existing)
                return Ok(new UploadResponse { Id = fileId, Existing = true });
            return StatusCode(201, new UploadResponse { Id = fileId, Existing = false });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "XrkApi request failed");
            return StatusCode(502, new { error = "XrkApi unavailable or failed.", detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");
            return StatusCode(500, new { error = "Upload failed.", detail = ex.Message });
        }
    }

    /// <summary>
    /// List files (id, file_hash, filename, vehicle, track, created_at).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FileListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _repo.ListFilesAsync(ct);
        var ids = items.Select(x => x.Id).ToList();
        var shortestMiddle = await _repo.GetShortestMiddleLapByFileIdsAsync(ids, ct);
        var result = items.Select(x => new FileListResponse
        {
            Id = x.Id,
            FileHash = x.FileHash,
            Filename = x.Filename,
            Vehicle = x.Vehicle,
            Track = x.Track,
            CreatedAt = x.CreatedAt,
            DateCreated = x.DateCreated,
            LastModified = x.LastModified,
            ShortestMiddleLapSec = shortestMiddle.TryGetValue(x.Id, out var sec) ? sec : null,
            LibraryDate = x.LibraryDate,
            LibraryTime = x.LibraryTime,
            LoggerId = x.LoggerId,
            LapCount = x.LapCount
        }).ToList();
        return Ok(result);
    }

    /// <summary>
    /// Get one file with laps and channels.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var details = await _repo.GetFileWithDetailsAsync(id, ct);
        if (details == null)
            return NotFound();

        var f = details.File;
        return Ok(new FileDetailResponse
        {
            Id = f.Id,
            FileHash = f.FileHash,
            Filename = f.Filename,
            LibraryDate = f.LibraryDate,
            LibraryTime = f.LibraryTime,
            Vehicle = f.Vehicle,
            Track = f.Track,
            Racer = f.Racer,
            LoggerId = f.LoggerId,
            LapCount = f.LapCount,
            CreatedAt = f.CreatedAt,
            DateCreated = f.DateCreated,
            LastModified = f.LastModified,
            Laps = details.Laps.Select(l => new LapResponse { Index = l.Index, Start = l.Start, Duration = l.Duration }).ToList(),
            Channels = details.Channels.Select(c => new ChannelResponse { Index = c.Index, Name = c.Name, Units = c.Units }).ToList(),
            GpsChannels = details.GpsChannels
        });
    }

    /// <summary>
    /// Delete a file and its CSV from storage. Returns 204 when deleted, 404 when not found.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _filesService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Stream CSV from storage. Supports CORS for frontend.
    /// </summary>
    [HttpGet("{id:guid}/csv")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCsv(Guid id, CancellationToken ct)
    {
        var storageKey = await _repo.GetCsvStorageKeyAsync(id, ct);
        if (storageKey == null)
            return NotFound();

        var volumePath = _csvOptions.Value.VolumePath?.Trim() ?? "/data/csv";
        var path = Path.Combine(volumePath, storageKey);
        if (!System.IO.File.Exists(path))
            return NotFound();

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return File(stream, "text/csv", Path.GetFileName(storageKey));
    }
}

public class UploadResponse
{
    public Guid Id { get; set; }
    public bool Existing { get; set; }
}

public class FileListResponse
{
    public Guid Id { get; set; }
    public string FileHash { get; set; } = "";
    public string? Filename { get; set; }
    public string? Vehicle { get; set; }
    public string? Track { get; set; }
    public DateTime CreatedAt { get; set; }
    public double? ShortestMiddleLapSec { get; set; }
    public string? LibraryDate { get; set; }
    public string? LibraryTime { get; set; }
    public long? LoggerId { get; set; }
    public int LapCount { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
}

public class FileDetailResponse
{
    public Guid Id { get; set; }
    public string FileHash { get; set; } = "";
    public string? Filename { get; set; }
    public string? LibraryDate { get; set; }
    public string? LibraryTime { get; set; }
    public string? Vehicle { get; set; }
    public string? Track { get; set; }
    public string? Racer { get; set; }
    public long? LoggerId { get; set; }
    public int LapCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
    public List<LapResponse> Laps { get; set; } = new();
    public List<ChannelResponse> Channels { get; set; } = new();
    public List<string> GpsChannels { get; set; } = new();
}

public class LapResponse
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}

public class ChannelResponse
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }
}
