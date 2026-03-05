using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using XrkApi;
using Xunit;

namespace XrkApi.Tests;

public class BuildCsvTests
{
    [Fact]
    public void BuildCsvIncludesExpectedHeader()
    {
        using var reader = new FakeXrkFileReader();
        var csv = XrkService.BuildCsv(reader);

        var firstLine = csv.AsSpan().Slice(0, csv.IndexOf('\n')).ToString();
        Assert.Equal("Time,Value,Channel,Units,Vehicle,Track", firstLine);
    }

    [Fact]
    public void BuildCsvIncludesOneRowPerSampleWithVehicleAndTrack()
    {
        using var reader = new FakeXrkFileReader();
        var csv = XrkService.BuildCsv(reader);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Header + 3 samples (Speed) + 2 samples (RPM) = 6 lines
        Assert.Equal(6, lines.Length);
        Assert.Equal("Time,Value,Channel,Units,Vehicle,Track", lines[0]);

        // First data row: time 0, value 10, Speed, km/h, TestVehicle, TestTrack
        Assert.Equal("0,10,Speed,km/h,TestVehicle,TestTrack", lines[1]);
        Assert.Equal("1,20,Speed,km/h,TestVehicle,TestTrack", lines[2]);
        Assert.Equal("2,30,Speed,km/h,TestVehicle,TestTrack", lines[3]);
        Assert.Equal("0,1000,RPM,rpm,TestVehicle,TestTrack", lines[4]);
        Assert.Equal("1,2000,RPM,rpm,TestVehicle,TestTrack", lines[5]);
    }

    [Fact]
    public void BuildCsvEmptyChannelsProducesHeaderOnly()
    {
        using var reader = new FakeXrkFileReader();
        reader.SetChannels(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<double[]>(), Array.Empty<double[]>());
        var csv = XrkService.BuildCsv(reader);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal("Time,Value,Channel,Units,Vehicle,Track", lines[0]);
    }

    [Fact]
    public void BuildCsvWhenChannelDataLengthsDiffer_EmitsOnlyValidPairs()
    {
        using var reader = new MismatchedLengthFakeReader();
        var csv = XrkService.BuildCsv(reader);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Equal("Time,Value,Channel,Units,Vehicle,Track", lines[0]);
        Assert.Equal("0,10,Speed,km/h,TestVehicle,TestTrack", lines[1]);
        Assert.Equal("1,20,Speed,km/h,TestVehicle,TestTrack", lines[2]);
    }
}

internal sealed class MismatchedLengthFakeReader : IXrkFileReader
{
    public string LibraryDate => "2022-01-01";
    public string LibraryTime => "12:00:00";
    public string GetVehicleName() => "TestVehicle";
    public string GetTrackName() => "TestTrack";
    public string GetRacerName() => "TestRacer";
    public int LapCount => 1;
    public IReadOnlyList<MatLabXrkWrapper.LapInfo> GetLaps() => [new MatLabXrkWrapper.LapInfo(1, 0.0, 100.0)];
    public List<string> GetChannelNames() => ["Speed"];
    public string GetChannelUnits(int channelIndex) => "km/h";
    public (double[] Times, double[] Values) GetChannelData(int channelIndex) => ([0.0, 1.0, 2.0], [10.0, 20.0]);
    public IReadOnlyList<string> GetGpsChannelNames() => [];
    public void Dispose() { }
}

public class FakeXrkFileReaderSetChannelsTests
{
    [Fact]
    public void SetChannels_WhenTimesAndValuesLengthDiffer_Throws()
    {
        using var reader = new FakeXrkFileReader();
        var ex = Assert.Throws<ArgumentException>(() => reader.SetChannels(
            ["A"], ["u"], new[] { new double[] { 0, 1, 2 } }, new[] { new double[] { 10, 20 } }));
        Assert.Contains("times and values must have the same length", ex.Message);
    }

    [Fact]
    public void SetChannels_WhenArrayLengthsDiffer_Throws()
    {
        using var reader = new FakeXrkFileReader();
        Assert.Throws<ArgumentException>(() => reader.SetChannels(
            ["A", "B"], ["u"], new double[][] { [0.0], [0.0] }, new double[][] { [1.0], [1.0] }));
    }

    [Fact]
    public void SetChannels_WhenNull_Throws()
    {
        using var reader = new FakeXrkFileReader();
        Assert.Throws<ArgumentNullException>(() => reader.SetChannels(null!, ["u"], new double[][] { [0.0] }, new double[][] { [1.0] }));
    }
}

public class BuildMetadataTests
{
    [Fact]
    public void BuildMetadataReturnsVehicleTrackRacerAndLibraryInfo()
    {
        using var reader = new FakeXrkFileReader();
        var metadata = XrkService.BuildMetadata(reader);

        var dict = GetAnonymousDictionary(metadata);
        Assert.Equal("TestVehicle", dict["Vehicle"]);
        Assert.Equal("TestTrack", dict["Track"]);
        Assert.Equal("TestRacer", dict["Racer"]);
        Assert.Equal("2022-01-01", dict["LibraryDate"]);
        Assert.Equal("12:00:00", dict["LibraryTime"]);
        Assert.Equal(2, dict["LapCount"]);
    }

    [Fact]
    public void BuildMetadataIncludesChannelsWithIndexNameAndUnits()
    {
        using var reader = new FakeXrkFileReader();
        var metadata = XrkService.BuildMetadata(reader);

        var dict = GetAnonymousDictionary(metadata);
        var channels = (System.Collections.IEnumerable)dict["Channels"]!;
        var list = channels.Cast<object>().ToList();
        Assert.Equal(2, list.Count);

        var ch0 = GetAnonymousDictionary(list[0]);
        Assert.Equal(0, ch0["Index"]);
        Assert.Equal("Speed", ch0["Name"]);
        Assert.Equal("km/h", ch0["Units"]);

        var ch1 = GetAnonymousDictionary(list[1]);
        Assert.Equal(1, ch1["Index"]);
        Assert.Equal("RPM", ch1["Name"]);
        Assert.Equal("rpm", ch1["Units"]);
    }

    [Fact]
    public void BuildMetadataIncludesLapsAndGpsChannels()
    {
        using var reader = new FakeXrkFileReader();
        var metadata = XrkService.BuildMetadata(reader);

        var dict = GetAnonymousDictionary(metadata);
        var laps = (System.Collections.IEnumerable)dict["Laps"]!;
        Assert.Equal(2, laps.Cast<object>().Count());

        var gps = (System.Collections.IEnumerable)dict["GpsChannels"]!;
        var gpsList = gps.Cast<string>().ToList();
        Assert.Equal(["Latitude", "Longitude"], gpsList);
    }

    private static Dictionary<string, object?> GetAnonymousDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in obj.GetType().GetProperties())
            dict[prop.Name] = prop.GetValue(obj);
        return dict;
    }
}

public class WithXrkFileAsyncTests
{
    [Fact]
    public async Task WithXrkFileAsyncEmptyFileReturnsBadRequest()
    {
        var emptyFile = new FormFile(Stream.Null, 0, 0, "file", "test.xrk");
        var result = await XrkService.WithXrkFileAsync(emptyFile, returnCsv: false);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task WithXrkFileAsyncEmptyFile_ReturnCsv_ReturnsBadRequest()
    {
        var emptyFile = new FormFile(Stream.Null, 0, 0, "file", "test.xrk");
        var result = await XrkService.WithXrkFileAsync(emptyFile, returnCsv: true);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<string>>(result);
    }

    [Fact]
    public async Task WithXrkFileAsyncInvalidFile_ReturnsErrorResult()
    {
        var invalidContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("not an xrk file"));
        var formFile = new FormFile(invalidContent, 0, invalidContent.Length, "file", "test.xrk");
        var result = await XrkService.WithXrkFileAsync(formFile, returnCsv: false, useCache: false);

        var typeName = result.GetType().Name;
        Assert.True(
            typeName.Contains("BadRequest", StringComparison.Ordinal) || typeName.Contains("Problem", StringComparison.Ordinal),
            $"Invalid file should return 400 or 500; got {typeName}.");
    }
}

public class CacheExistsTests
{
    private const string ValidKey64 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public void CacheExists_Null_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists(null!));
    }

    [Fact]
    public void CacheExists_EmptyString_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists(""));
    }

    [Fact]
    public void CacheExists_Whitespace_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists("   "));
    }

    [Fact]
    public void CacheExists_InvalidLength_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists("ABC"));
        Assert.False(XrkService.CacheExists(ValidKey64 + "0"));
    }

    [Fact]
    public void CacheExists_InvalidCharacters_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists(new string('G', 64)));
        Assert.False(XrkService.CacheExists("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"));
    }

    [Fact]
    public void CacheExists_ValidKeyNotInCache_ReturnsFalse()
    {
        Assert.False(XrkService.CacheExists(ValidKey64));
    }

    [Fact]
    public void CacheExists_ValidKeyInCache_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "XrkApi.Tests", Guid.NewGuid().ToString("N"));
        var prevDir = Environment.GetEnvironmentVariable("XRK_CACHE_DIR");
        try
        {
            Directory.CreateDirectory(tempDir);
            Environment.SetEnvironmentVariable("XRK_CACHE_DIR", tempDir, EnvironmentVariableTarget.Process);
            File.WriteAllText(Path.Combine(tempDir, $"{ValidKey64}.metadata.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, $"{ValidKey64}.csv"), "x");

            Assert.True(XrkService.CacheExists(ValidKey64));
        }
        finally
        {
            Environment.SetEnvironmentVariable("XRK_CACHE_DIR", prevDir ?? "", EnvironmentVariableTarget.Process);
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { /* ignore */ }
        }
    }
}

public class GetCacheDirectoryTests
{
    [Fact]
    public void GetCacheDirectory_ReturnsNonEmptyPath()
    {
        var path = XrkService.GetCacheDirectory();
        Assert.False(string.IsNullOrWhiteSpace(path));
    }
}

public class GetCacheEntryCountTests
{
    [Fact]
    public void GetCacheEntryCount_ReturnsNonNegative()
    {
        var count = XrkService.GetCacheEntryCount();
        Assert.True(count >= 0);
    }
}

public class IsLocalOrPrivateAddressTests
{
    [Fact]
    public void IsLocalOrPrivateAddress_Null_ReturnsFalse()
    {
        Assert.False(XrkService.IsLocalOrPrivateAddress(null));
    }

    [Fact]
    public void IsLocalOrPrivateAddress_Loopback_ReturnsTrue()
    {
        Assert.True(XrkService.IsLocalOrPrivateAddress(IPAddress.Loopback));
    }
}

public class GetHealthResultTests
{
    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Test";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public void GetHealthResult_ReturnsNonNullResult()
    {
        var env = new StubHostEnvironment();
        var result = XrkService.GetHealthResult(env);
        Assert.NotNull(result);
    }
}
