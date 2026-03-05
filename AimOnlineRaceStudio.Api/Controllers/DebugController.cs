using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly IXrkApiClient _xrkApiClient;
    private readonly IOptions<XrkApiOptions> _xrkApiOptions;
    private readonly IFilesRepository _filesRepository;
    private readonly IFilesService _filesService;

    [ActivatorUtilitiesConstructor]
    public DebugController(
        IXrkApiClient xrkApiClient,
        IOptions<XrkApiOptions> xrkApiOptions,
        IFilesRepository filesRepository,
        IFilesService filesService)
    {
        _xrkApiClient = xrkApiClient;
        _xrkApiOptions = xrkApiOptions;
        _filesRepository = filesRepository;
        _filesService = filesService;
    }

    [HttpGet("xrkapi-health")]
    public async Task<IActionResult> GetXrkApiHealth(CancellationToken ct)
    {
        var baseUrl = _xrkApiOptions.Value.BaseUrl ?? "http://10.0.0.44:5000";
        var result = await _xrkApiClient.GetHealthAsync(ct);
        return Ok(new
        {
            configuredUrl = baseUrl,
            result,
        });
    }

    [HttpGet("storage")]
    public async Task<IActionResult> GetStorageStats(CancellationToken ct)
    {
        var (totalBytes, fileCount) = await _filesRepository.GetStorageStatsAsync(ct);
        return Ok(new { totalBytes, fileCount });
    }

    [HttpDelete("cache")]
    public async Task<IActionResult> ClearCacheAll(CancellationToken ct)
    {
        var (ok, cleared) = await _xrkApiClient.ClearCacheAllAsync(ct);
        if (!ok)
            return StatusCode(502, new { error = "XrkApi request failed", cleared = 0 });
        return Ok(new { cleared });
    }

    [HttpDelete("cache/{key}")]
    public async Task<IActionResult> ClearCacheEntry([FromRoute] string key, CancellationToken ct)
    {
        var (ok, removed) = await _xrkApiClient.ClearCacheEntryAsync(key, ct);
        if (!ok)
            return StatusCode(502, new { error = "XrkApi request failed", removed = false });
        return removed ? Ok(new { key, removed = true }) : NotFound(new { key, removed = false });
    }

    [HttpPost("clear-all")]
    public async Task<IActionResult> ClearAll(CancellationToken ct)
    {
        var count = await _filesService.ClearAllAsync(ct);
        return Ok(new { deleted = count });
    }
}
