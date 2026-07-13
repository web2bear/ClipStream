using System.Windows.Threading;

namespace ClipStream.Clipboard.Tests;

internal static class ClipboardMessagePump
{
    public static Task WaitUntilAsync(
        Dispatcher dispatcher,
        Func<bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return Task.CompletedTask;
            }

            Pump(dispatcher);
            Thread.Sleep(20);
        }

        throw new TimeoutException("Condition was not met before timeout.");
    }

    public static void Pump(Dispatcher dispatcher)
    {
        dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }
}
