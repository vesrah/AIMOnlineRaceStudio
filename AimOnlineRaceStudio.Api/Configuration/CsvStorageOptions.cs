namespace AimOnlineRaceStudio.Api.Configuration;

public class CsvStorageOptions
{
    public const string SectionName = "CsvStorage";
    /// <summary>Directory path for CSV files (e.g. /data/csv). Storage key is {file_hash}.csv under this path.</summary>
    public string VolumePath { get; set; } = "/data/csv";
}
