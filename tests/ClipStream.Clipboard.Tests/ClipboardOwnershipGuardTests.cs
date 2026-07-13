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
}
