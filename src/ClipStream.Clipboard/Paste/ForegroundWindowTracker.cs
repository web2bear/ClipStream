using System.Runtime.InteropServices;
using ClipStream.Clipboard.Win32;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipStream.Clipboard.Paste;

public interface IForegroundWindowTracker
{
    IntPtr LastExternalForeground { get; }
}

public sealed class ForegroundWindowTracker : IForegroundWindowTracker, IHostedService
{
    private readonly ILogger<ForegroundWindowTracker> _logger;
    private readonly uint _currentProcessId;
    private readonly WinEventHook.WinEventDelegate _callback;
    private IntPtr _hook;
    private IntPtr _lastExternalForeground;

    public ForegroundWindowTracker(ILogger<ForegroundWindowTracker> logger)
    {
        _logger = logger;
        _currentProcessId = Kernel32.GetCurrentProcessId();
        _callback = OnForegroundChanged;
    }

    public IntPtr LastExternalForeground => Volatile.Read(ref _lastExternalForeground);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        RememberIfExternal(User32.GetForegroundWindow());

        _hook = WinEventHook.InstallForegroundHook(_callback);
        if (_hook == IntPtr.Zero)
        {
            _logger.LogWarning("Failed to install foreground window hook.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hook != IntPtr.Zero)
        {
            _ = WinEventHook.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        return Task.CompletedTask;
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        RememberIfExternal(hwnd);
    }

    private void RememberIfExternal(IntPtr hwnd)
    {
        if (!WindowActivator.IsValidWindow(hwnd))
        {
            return;
        }

        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == _currentProcessId)
        {
            return;
        }

        Volatile.Write(ref _lastExternalForeground, hwnd);
    }
}
