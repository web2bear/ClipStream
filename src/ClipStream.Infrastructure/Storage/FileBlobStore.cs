using System.Security.Cryptography;
using ClipStream.Core.Storage;

namespace ClipStream.Infrastructure.Storage;

public sealed class FileBlobStore : IBlobStore
{
    private readonly string _rootPath;

    public FileBlobStore()
    {
        _rootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipStream",
            "blobs");
        Directory.CreateDirectory(_rootPath);
    }

    public FileBlobStore(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var hash = ComputeHash(data);
        var storageKey = $"{hash[..2]}/{hash}";
        var fullPath = GetFullPath(storageKey);

        if (File.Exists(fullPath))
        {
            return storageKey;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);
        return storageKey;
    }

    public async Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(GetFullPath(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetFullPath(string storageKey) => Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

    public static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
