using System.Net.Http.Headers;
using AimOnlineRaceStudio.Api.Configuration;
using AimOnlineRaceStudio.Api.Models;
using Microsoft.Extensions.Options;

namespace AimOnlineRaceStudio.Api.Services;

public interface IXrkApiClient
{
    Task<XrkMetadataDto?> GetMetadataAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task StreamCsvToFileAsync(Stream fileStream, string fileName, string destinationPath, CancellationToken ct = default);
    Task<XrkApiHealthResponse?> GetHealthAsync(CancellationToken ct = default);
    Task<(bool Ok, bool Removed)> ClearCacheEntryAsync(string key, CancellationToken ct = default);
    Task<(bool Ok, int Cleared)> ClearCacheAllAsync(CancellationToken ct = default);
}

public class XrkApiClient : IXrkApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<XrkApiClient> _logger;

    public XrkApiClient(HttpClient httpClient, IOptions<XrkApiOptions> options, ILogger<XrkApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        var opts = options.Value;
        var baseUrl = opts.BaseUrl?.TrimEnd('/') ?? "http://10.0.0.44:5000";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // large file conversion can take time
        var token = opts.SharedToken?.Trim();
        if (!string.IsNullOrEmpty(token))
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<XrkMetadataDto?> GetMetadataAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        fileStream.Position = 0;
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync("/metadata", content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return System.Text.Json.JsonSerializer.Deserialize<XrkMetadataDto>(json);
    }

    /// <summary>
    /// POST file to XrkApi /csv and stream response directly to destination file (no full buffer in memory).
    /// </summary>
    public async Task StreamCsvToFileAsync(Stream fileStream, string fileName, string destinationPath, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        fileStream.Position = 0;
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/csv") { Content = content };
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await using var fileStreamOut = File.Create(destinationPath);
        await responseStream.CopyToAsync(fileStreamOut, ct);
    }

    public async Task<XrkApiHealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try
        {
            using var response = await _httpClient.GetAsync("/health", linkedCts.Token);
            var json = await response.Content.ReadAsStringAsync(linkedCts.Token);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return new XrkApiHealthResponse
            {
                Reachable = true,
                StatusCode = (int)response.StatusCode,
                Ok = response.IsSuccessStatusCode,
                Body = doc.RootElement.Clone(),
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("XrkApi health check timed out after 5s");
            return new XrkApiHealthResponse { Reachable = false, Error = "Connection timed out (5s)" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XrkApi health check failed");
            return new XrkApiHealthResponse
            {
                Reachable = false,
                Error = ex.Message,
            };
        }
    }

    public async Task<(bool Ok, bool Removed)> ClearCacheEntryAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync($"/cache/{Uri.EscapeDataString(key)}", ct);
            if (!response.IsSuccessStatusCode)
                return (false, false);
            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var removed = doc.RootElement.TryGetProperty("removed", out var r) && r.GetBoolean();
            return (true, removed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XrkApi clear cache entry failed");
            return (false, false);
        }
    }

    public async Task<(bool Ok, int Cleared)> ClearCacheAllAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.DeleteAsync("/cache", ct);
            if (!response.IsSuccessStatusCode)
                return (false, 0);
            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var cleared = doc.RootElement.TryGetProperty("cleared", out var c) ? c.GetInt32() : 0;
            return (true, cleared);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XrkApi clear cache all failed");
            return (false, 0);
        }
    }
}
