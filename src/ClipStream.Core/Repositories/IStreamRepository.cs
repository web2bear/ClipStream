using ClipStream.Core.Models;

namespace ClipStream.Core.Repositories;

public interface IStreamRepository
{
    Task<IReadOnlyList<ClipStreamEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ClipStreamEntity?> GetByIdAsync(Guid streamId, CancellationToken cancellationToken = default);

    Task<ClipStreamEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task SaveAsync(ClipStreamEntity stream, CancellationToken cancellationToken = default);

    Task EnsureDefaultStreamAsync(CancellationToken cancellationToken = default);
}
