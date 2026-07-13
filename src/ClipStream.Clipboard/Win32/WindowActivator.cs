using System.Runtime.InteropServices;

namespace ClipStream.Clipboard.Win32;

internal static class WindowActivator
{
    private const int SwRestore = 9;

    public static bool IsValidWindow(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && User32.IsWindow(hwnd);

    public static bool TryActivate(IntPtr hwnd)
    {
        if (!IsValidWindow(hwnd))
        {
            return false;
        }

        if (User32.IsIconic(hwnd))
        {
            _ = User32.ShowWindow(hwnd, SwRestore);
        }

        _ = User32.GetWindowThreadProcessId(hwnd, out var targetProcessId);
        _ = User32.AllowSetForegroundWindow(targetProcessId);

        var currentForeground = User32.GetForegroundWindow();
        var currentThread = User32.GetWindowThreadProcessId(currentForeground, out _);
        var targetThread = User32.GetWindowThreadProcessId(hwnd, out _);
        var attached = false;

        if (currentThread != targetThread)
        {
            attached = User32.AttachThreadInput(currentThread, targetThread, true);
        }

        try
        {
            _ = User32.SetForegroundWindow(hwnd);
            _ = User32.BringWindowToTop(hwnd);
        }
        finally
        {
            if (attached)
            {
                _ = User32.AttachThreadInput(currentThread, targetThread, false);
            }
        }

        return User32.GetForegroundWindow() == hwnd;
    }
}
