using ClipStream.Core.Storage;

namespace ClipStream.Export;

public sealed class AttachmentCopier
{
    private readonly IBlobStore _blobStore;

    public AttachmentCopier(IBlobStore blobStore) => _blobStore = blobStore;

    public async Task<string?> CopyAsync(
        string storageKey,
        string exportRoot,
        string? preferredExtension = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _blobStore.GetAsync(storageKey, cancellationToken);
        if (data is null)
        {
            return null;
        }

        var hash = ContentHashHelper.ComputeBlobHash(data);
        var prefix = hash[..2];
        var extension = preferredExtension ?? GuessExtension(data);
        var relativePath = Path.Combine("attachments", prefix, $"{hash}{extension}");
        var fullPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            return relativePath.Replace('\\', '/');
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);
        return relativePath.Replace('\\', '/');
    }

    public async Task<string?> CopyRawAsync(
        string storageKey,
        string formatName,
        string exportRoot,
        CancellationToken cancellationToken = default)
    {
        var data = await _blobStore.GetAsync(storageKey, cancellationToken);
        if (data is null)
        {
            return null;
        }

        var hash = ContentHashHelper.ComputeBlobHash(data);
        var safeFormat = SlugGenerator.FromText(formatName, 30);
        var relativePath = Path.Combine("attachments", "raw", $"{hash}.{safeFormat}.bin");
        var fullPath = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            return relativePath.Replace('\\', '/');
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);
        return relativePath.Replace('\\', '/');
    }

    private static string GuessExtension(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50)
        {
            return ".png";
        }

        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
        {
            return ".jpg";
        }

        if (data.Length >= 4 && data[0] == 0x42 && data[1] == 0x4D)
        {
            return ".bmp";
        }

        return ".bin";
    }
}

internal static class ContentHashHelper
{
    public static string ComputeBlobHash(byte[] data)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
