using ClipStream.Core.Models;

namespace ClipStream.Core.Repositories;

public interface IFragmentRepository
{
    event EventHandler<FragmentAddedEventArgs>? FragmentAdded;

    Task SaveAsync(ClipboardFragment fragment, Guid streamId, CancellationToken cancellationToken = default);

    Task<ClipboardFragment?> GetByIdAsync(Guid fragmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipboardFragment>> GetByStreamAsync(
        Guid streamId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipboardFragment>> GetAllAsync(
        int skip = 0,
        int take = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipboardFragment>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task<Guid?> GetStreamIdForFragmentAsync(Guid fragmentId, CancellationToken cancellationToken = default);

    Task MoveToStreamAsync(Guid fragmentId, Guid targetStreamId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);

    Task UpdateTitleAsync(Guid fragmentId, string title, CancellationToken cancellationToken = default);
}

public sealed class FragmentAddedEventArgs : EventArgs
{
    public required ClipboardFragment Fragment { get; init; }

    public required Guid StreamId { get; init; }
}
