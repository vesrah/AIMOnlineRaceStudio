using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Models;
using AimOnlineRaceStudio.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Tests;

public class FakeFilesRepository : IFilesRepository
{
    private readonly Dictionary<string, Guid> _hashToId = new();
    private bool _nextInsertConflicts;

    public void SeedFile(string hash, Guid id) => _hashToId[hash] = id;
    public void SimulateConflictOnNextInsert() => _nextInsertConflicts = true;

    public int InsertCallCount { get; private set; }

    public Task<Guid?> GetFileIdByHashAsync(string fileHash, CancellationToken ct = default)
        => Task.FromResult(_hashToId.TryGetValue(fileHash, out var id) ? (Guid?)id : null);

    public Task<FileRecord?> GetFileByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<FileRecord?>(null);

    public Task<List<FileListItem>> ListFilesAsync(CancellationToken ct = default)
        => Task.FromResult(new List<FileListItem>());

    public Task<Dictionary<Guid, double>> GetShortestMiddleLapByFileIdsAsync(IReadOnlyList<Guid> fileIds, CancellationToken ct = default)
        => Task.FromResult(new Dictionary<Guid, double>());

    public Task<(bool Deleted, string? StorageKey)> DeleteFileAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult((false, (string?)null));

    public Task<FileWithDetails?> GetFileWithDetailsAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<FileWithDetails?>(null);

    public Task<string?> GetCsvStorageKeyAsync(Guid fileId, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<(Guid Id, bool Conflict)> InsertFileAsync(FileRecord file, XrkMetadataDto metadata, string csvStorageKey, long? csvByteSize, CancellationToken ct = default)
    {
        InsertCallCount++;
        if (_nextInsertConflicts)
        {
            _nextInsertConflicts = false;
            var existingId = Guid.NewGuid();
            _hashToId[file.FileHash] = existingId;
            return Task.FromResult((existingId, Conflict: true));
        }
        _hashToId[file.FileHash] = file.Id;
        return Task.FromResult((file.Id, Conflict: false));
    }
}

public class FakeXrkApiClient : IXrkApiClient
{
    public Task<XrkMetadataDto?> GetMetadataAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        return Task.FromResult<XrkMetadataDto?>(new XrkMetadataDto
        {
            Vehicle = "TestCar",
            Track = "TestTrack",
            Racer = "TestDriver",
            LapCount = 1,
            Laps = [new LapDto { Index = 1, Start = 0, Duration = 60 }],
            Channels = [new ChannelDto { Index = 0, Name = "Speed", Units = "km/h" }],
            GpsChannels = ["Latitude"]
        });
    }

    public Task StreamCsvToFileAsync(Stream fileStream, string fileName, string destinationPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllText(destinationPath, "Time,Value\n0,100\n");
        return Task.CompletedTask;
    }

    public Task<XrkApiHealthResponse?> GetHealthAsync(CancellationToken ct = default)
        => Task.FromResult<XrkApiHealthResponse?>(new XrkApiHealthResponse { Reachable = true, Ok = true });

    public Task<(bool Ok, bool Removed)> ClearCacheEntryAsync(string key, CancellationToken ct = default)
        => Task.FromResult((true, true));

    public Task<(bool Ok, int Cleared)> ClearCacheAllAsync(CancellationToken ct = default)
        => Task.FromResult((true, 0));
}

public class FilesServiceTests : IDisposable
{
    private readonly string _tempCsvDir;
    private readonly FakeFilesRepository _repo;
    private readonly FilesService _service;

    public FilesServiceTests()
    {
        _tempCsvDir = Path.Combine(Path.GetTempPath(), "AimTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempCsvDir);
        _repo = new FakeFilesRepository();
        var csvOptions = Options.Create(new CsvStorageOptions { VolumePath = _tempCsvDir });
        _service = new FilesService(
            _repo,
            new FakeXrkApiClient(),
            csvOptions,
            NullLogger<FilesService>.Instance);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempCsvDir)) Directory.Delete(_tempCsvDir, recursive: true); } catch { }
    }

    private static MemoryStream MakeStream(string content = "fake xrk data")
        => new(System.Text.Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Upload_NewFile_ReturnsNewIdAndNotExisting()
    {
        using var stream = MakeStream();
        var (fileId, existing) = await _service.UploadAsync(stream, "test.xrk");

        Assert.False(existing);
        Assert.NotEqual(Guid.Empty, fileId);
        Assert.Equal(1, _repo.InsertCallCount);
    }

    [Fact]
    public async Task Upload_SameHash_ReturnsExistingWithoutInsert()
    {
        using var stream1 = MakeStream("same content");
        var (firstId, firstExisting) = await _service.UploadAsync(stream1, "a.xrk");
        Assert.False(firstExisting);

        using var stream2 = MakeStream("same content");
        var (secondId, secondExisting) = await _service.UploadAsync(stream2, "a.xrk");
        Assert.True(secondExisting);
        Assert.Equal(firstId, secondId);
        Assert.Equal(1, _repo.InsertCallCount);
    }

    [Fact]
    public async Task Upload_ConcurrentConflict_ReturnsExistingGracefully()
    {
        _repo.SimulateConflictOnNextInsert();
        using var stream = MakeStream("conflict content");
        var (fileId, existing) = await _service.UploadAsync(stream, "conflict.xrk");

        Assert.True(existing);
        Assert.NotEqual(Guid.Empty, fileId);
        Assert.Equal(1, _repo.InsertCallCount);
    }

    [Fact]
    public async Task Upload_DifferentFiles_GetDifferentIds()
    {
        using var stream1 = MakeStream("file one");
        var (id1, _) = await _service.UploadAsync(stream1, "one.xrk");

        using var stream2 = MakeStream("file two");
        var (id2, _) = await _service.UploadAsync(stream2, "two.xrk");

        Assert.NotEqual(id1, id2);
        Assert.Equal(2, _repo.InsertCallCount);
    }
}
