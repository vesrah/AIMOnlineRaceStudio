namespace XrkApi;

public interface IXrkFileReader : IDisposable
{
    string LibraryDate { get; }
    string LibraryTime { get; }
    string GetVehicleName();
    string GetTrackName();
    string GetRacerName();
    string GetChampionshipName();
    string GetSessionTypeName();
    double? GetSessionDurationSeconds();
    uint? GetLoggerId();
    int LapCount { get; }
    IReadOnlyList<MatLabXrkWrapper.LapInfo> GetLaps();
    List<string> GetChannelNames();
    string GetChannelUnits(int channelIndex);
    (double[] Times, double[] Values) GetChannelData(int channelIndex);
    IReadOnlyList<string> GetGpsChannelNames();
}
