using ClipStream.Clipboard.Guard;

namespace ClipStream.Clipboard.Tests;

public class ClipboardOwnershipGuardTests
{
    [Fact]
    public void SuppressListening_SetsIsOwnWrite()
    {
        var guard = new ClipboardOwnershipGuard();
        Assert.False(guard.IsOwnWrite);

        using (guard.SuppressListening())
        {
            Assert.True(guard.IsOwnWrite);
        }

        Assert.False(guard.IsOwnWrite);
    }

    [Fact]
    public void MarkOwnSequence_IsIgnoredUntilExternalSequence()
    {
        var guard = new ClipboardOwnershipGuard();
        guard.MarkOwnSequence(42);

        Assert.Equal(42u, guard.SuppressedSequence);
        Assert.True(guard.ShouldIgnoreSequence(42));
        Assert.False(guard.ShouldIgnoreSequence(43));

        guard.ClearSuppressedSequenceIfExternal(43);
        Assert.Null(guard.SuppressedSequence);
        Assert.False(guard.ShouldIgnoreSequence(42));
    }

    [Fact]
    public void ClearSuppressedSequenceIfExternal_KeepsSameSequence()
    {
        var guard = new ClipboardOwnershipGuard();
        guard.MarkOwnSequence(7);

        guard.ClearSuppressedSequenceIfExternal(7);
        Assert.Equal(7u, guard.SuppressedSequence);
        Assert.True(guard.ShouldIgnoreSequence(7));
    }

    [Fact]
    public void ShouldIgnoreSequence_TrueWhileSuppressing()
    {
        var guard = new ClipboardOwnershipGuard();
        using (guard.SuppressListening())
        {
            Assert.True(guard.ShouldIgnoreSequence(1));
            Assert.True(guard.ShouldIgnoreSequence(999));
        }
    }
}
