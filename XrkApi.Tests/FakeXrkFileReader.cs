using XrkApi;

namespace XrkApi.Tests;

/// <summary>
/// Fake XRK reader that returns fixed data for unit testing BuildCsv and BuildMetadata without the native DLL.
/// </summary>
public sealed class FakeXrkFileReader : IXrkFileReader
{
    public string LibraryDate { get; set; } = "2022-01-01";
    public string LibraryTime { get; set; } = "12:00:00";
    public string VehicleName { get; set; } = "TestVehicle";
    public string TrackName { get; set; } = "TestTrack";
    public string RacerName { get; set; } = "TestRacer";

    private List<string> _channelNames = ["Speed", "RPM"];
    private List<string> _channelUnits = ["km/h", "rpm"];
    private List<double[]> _times =
    [
        [0.0, 1.0, 2.0],
        [0.0, 1.0]
    ];
    private List<double[]> _values =
    [
        [10.0, 20.0, 30.0],
        [1000.0, 2000.0]
    ];
    private readonly List<string> _gpsChannels = ["Latitude", "Longitude"];

    /// <summary>Configure channels for tests (e.g. empty for header-only CSV).</summary>
    /// <exception cref="ArgumentNullException">If any of names, units, times, or values is null.</exception>
    /// <exception cref="ArgumentException">If array lengths do not match or any times[i].Length != values[i].Length.</exception>
    public void SetChannels(string[] names, string[] units, double[][] times, double[][] values)
    {
        if (names is null || units is null || times is null || values is null)
            throw new ArgumentNullException(names is null ? nameof(names) : units is null ? nameof(units) : times is null ? nameof(times) : nameof(values));
        var n = names.Length;
        if (units.Length != n || times.Length != n || values.Length != n)
            throw new ArgumentException("names, units, times, and values must have the same length.");
        for (var i = 0; i < n; i++)
        {
            if (times[i]?.Length != values[i]?.Length)
                throw new ArgumentException($"Channel {i}: times and values must have the same length.");
        }
        _channelNames = names.ToList();
        _channelUnits = units.ToList();
        _times = times.ToList();
        _values = values.ToList();
    }

    public string GetVehicleName() => VehicleName;
    public string GetTrackName() => TrackName;
    public string GetRacerName() => RacerName;
    public string GetChampionshipName() => "";
    public string GetSessionTypeName() => "";
    public double? GetSessionDurationSeconds() => 100.0;
    public uint? GetLoggerId() => 12345u;
    public int LapCount => 2;
    public IReadOnlyList<MatLabXrkWrapper.LapInfo> GetLaps() =>
        [new MatLabXrkWrapper.LapInfo(1, 0.0, 50.0), new MatLabXrkWrapper.LapInfo(2, 50.0, 50.0)];
    public List<string> GetChannelNames() => new(_channelNames);
    public string GetChannelUnits(int channelIndex) => _channelUnits[channelIndex];
    public (double[] Times, double[] Values) GetChannelData(int channelIndex) => (_times[channelIndex], _values[channelIndex]);
    public IReadOnlyList<string> GetGpsChannelNames() => _gpsChannels;

    public void Dispose() { }
}
