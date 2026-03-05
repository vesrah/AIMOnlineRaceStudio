using AimOnlineRaceStudio.Api.Services;

namespace AimOnlineRaceStudio.Api.Tests;

public class FileHashServiceTests
{
    [Fact]
    public async Task ComputeHash_ReturnsUppercaseHex64()
    {
        using var stream = new MemoryStream("hello"u8.ToArray());
        var hash = await FileHashService.ComputeSha256UppercaseHexAsync(stream);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToUpperInvariant());
        Assert.All(hash, c => Assert.True(char.IsAsciiHexDigitUpper(c) || char.IsDigit(c)));
    }

    [Fact]
    public async Task ComputeHash_SameContent_SameHash()
    {
        using var s1 = new MemoryStream("same"u8.ToArray());
        using var s2 = new MemoryStream("same"u8.ToArray());
        var h1 = await FileHashService.ComputeSha256UppercaseHexAsync(s1);
        var h2 = await FileHashService.ComputeSha256UppercaseHexAsync(s2);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public async Task ComputeHash_DifferentContent_DifferentHash()
    {
        using var s1 = new MemoryStream("aaa"u8.ToArray());
        using var s2 = new MemoryStream("bbb"u8.ToArray());
        var h1 = await FileHashService.ComputeSha256UppercaseHexAsync(s1);
        var h2 = await FileHashService.ComputeSha256UppercaseHexAsync(s2);

        Assert.NotEqual(h1, h2);
    }
}
