namespace ClipStream.Clipboard.Guard;

public interface IClipboardOwnershipGuard
{
    bool IsOwnWrite { get; }

    uint? SuppressedSequence { get; }

    IDisposable SuppressListening();

    void MarkOwnSequence(uint sequence);

    bool ShouldIgnoreSequence(uint sequence);

    void ClearSuppressedSequenceIfExternal(uint sequence);
}

public sealed class ClipboardOwnershipGuard : IClipboardOwnershipGuard
{
    private const long NoSequence = -1;

    private int _suppressDepth;
    private long _suppressedSequence = NoSequence;

    public bool IsOwnWrite => Volatile.Read(ref _suppressDepth) > 0;

    public uint? SuppressedSequence
    {
        get
        {
            var value = Interlocked.Read(ref _suppressedSequence);
            return value == NoSequence ? null : (uint)value;
        }
    }

    public IDisposable SuppressListening() => new SuppressScope(this);

    public void MarkOwnSequence(uint sequence) =>
        Interlocked.Exchange(ref _suppressedSequence, sequence);

    public bool ShouldIgnoreSequence(uint sequence)
    {
        if (IsOwnWrite)
        {
            return true;
        }

        var suppressed = Interlocked.Read(ref _suppressedSequence);
        return suppressed != NoSequence && (uint)suppressed == sequence;
    }

    public void ClearSuppressedSequenceIfExternal(uint sequence)
    {
        var suppressed = Interlocked.Read(ref _suppressedSequence);
        if (suppressed != NoSequence && (uint)suppressed != sequence)
        {
            Interlocked.CompareExchange(ref _suppressedSequence, NoSequence, suppressed);
        }
    }

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
