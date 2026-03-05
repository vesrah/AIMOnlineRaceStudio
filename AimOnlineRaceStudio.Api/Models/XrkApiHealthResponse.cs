using System.Text.Json;

namespace AimOnlineRaceStudio.Api.Models;

public class XrkApiHealthResponse
{
    public bool Reachable { get; set; }
    public int? StatusCode { get; set; }
    public bool Ok { get; set; }
    public JsonElement? Body { get; set; }
    public string? Error { get; set; }
}
