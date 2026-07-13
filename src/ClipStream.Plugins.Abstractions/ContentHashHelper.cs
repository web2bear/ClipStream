using System.Security.Cryptography;

namespace ClipStream.Plugins.Abstractions;

public static class ContentHashHelper
{
    public static string ComputeCaptureHash(IReadOnlyList<RawFormatData> formats)
    {
        using var sha = SHA256.Create();
        using var stream = new MemoryStream();
        foreach (var format in formats.OrderBy(f => f.FormatName, StringComparer.OrdinalIgnoreCase))
        {
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(format.FormatName);
            stream.Write(nameBytes);
            stream.Write(format.Data);
        }

        stream.Position = 0;
        var hash = sha.ComputeHash(stream);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    public static string ComputeBlobHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
