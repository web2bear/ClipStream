namespace ClipStream.Core.Storage;

public interface IBlobStore
{
    Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default);

    Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
