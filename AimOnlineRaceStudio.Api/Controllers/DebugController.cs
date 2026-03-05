using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly IXrkApiClient _xrkApiClient;
    private readonly IOptions<XrkApiOptions> _xrkApiOptions;

    public DebugController(IXrkApiClient xrkApiClient, IOptions<XrkApiOptions> xrkApiOptions)
    {
        _xrkApiClient = xrkApiClient;
        _xrkApiOptions = xrkApiOptions;
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
}
