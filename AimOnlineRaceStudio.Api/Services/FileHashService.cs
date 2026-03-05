using System.Security.Cryptography;
using System.Text;

namespace AimOnlineRaceStudio.Api.Services;

/// <summary>
/// SHA-256 hash of raw file bytes, 64-char uppercase hex (matches XrkApi).
/// </summary>
public static class FileHashService
{
    public static async Task<string> ComputeSha256UppercaseHexAsync(Stream stream, CancellationToken ct = default)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
