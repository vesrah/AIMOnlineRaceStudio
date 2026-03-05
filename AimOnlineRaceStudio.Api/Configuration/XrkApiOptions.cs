namespace AimOnlineRaceStudio.Api.Configuration;

public class XrkApiOptions
{
    public const string SectionName = "XrkApi";
    public string BaseUrl { get; set; } = "http://10.0.0.44:5000";
    /// <summary>Shared secret for XrkApi auth. When set, backend sends Authorization: Bearer &lt;token&gt; to XrkApi. Must match XrkApi's XRK_API_SHARED_TOKEN.</summary>
    public string? SharedToken { get; set; }
}
