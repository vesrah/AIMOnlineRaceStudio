using System.Text.Json.Serialization;

namespace AimOnlineRaceStudio.Api.Models;

/// <summary>
/// Shape of metadata from XrkApi POST /metadata (see docs/xrk-metadata-example.json).
/// </summary>
public class XrkMetadataDto
{
    [JsonPropertyName("libraryDate")]
    public string? LibraryDate { get; set; }

    [JsonPropertyName("libraryTime")]
    public string? LibraryTime { get; set; }

    [JsonPropertyName("vehicle")]
    public string? Vehicle { get; set; }

    [JsonPropertyName("track")]
    public string? Track { get; set; }

    [JsonPropertyName("racer")]
    public string? Racer { get; set; }

    [JsonPropertyName("lapCount")]
    public int LapCount { get; set; }

    [JsonPropertyName("laps")]
    public List<LapDto>? Laps { get; set; }

    [JsonPropertyName("channels")]
    public List<ChannelDto>? Channels { get; set; }

    [JsonPropertyName("gpsChannels")]
    public List<string>? GpsChannels { get; set; }
}

public class LapDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}

public class ChannelDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("units")]
    public string? Units { get; set; }
}
