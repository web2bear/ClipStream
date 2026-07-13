namespace ClipStream.Clipboard.Guard;

public interface IClipboardOwnershipGuard
{
    bool IsOwnWrite { get; }

    IDisposable SuppressListening();
}

public sealed class ClipboardOwnershipGuard : IClipboardOwnershipGuard
{
    private int _suppressDepth;

    public bool IsOwnWrite => Volatile.Read(ref _suppressDepth) > 0;

    public IDisposable SuppressListening() => new SuppressScope(this);

    private sealed class SuppressScope : IDisposable
    {
        private readonly ClipboardOwnershipGuard _guard;

        public SuppressScope(ClipboardOwnershipGuard guard)
        {
            _guard = guard;
            Interlocked.Increment(ref _guard._suppressDepth);
        }

        public void Dispose() => Interlocked.Decrement(ref _guard._suppressDepth);
    }
}
