using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClipStream.Clipboard.Guard;
using ClipStream.Clipboard.Win32;
using Microsoft.Extensions.Logging;

namespace ClipStream.Clipboard.Listener;

public sealed class ClipboardChangedEventArgs : EventArgs
{
    public uint SequenceNumber { get; init; }
}

public interface IClipboardListener : IAsyncDisposable
{
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    IntPtr ListenerHwnd { get; }

    void Initialize(IntPtr hwnd);
}

public sealed class Win32ClipboardListener : IClipboardListener
{
    private readonly IClipboardOwnershipGuard _ownershipGuard;
    private readonly ILogger<Win32ClipboardListener> _logger;
    private IntPtr _hwnd;
    private uint _lastSequence;
    private System.Windows.Interop.HwndSource? _source;
    private DispatcherTimer? _debounceTimer;

    public Win32ClipboardListener(
        IClipboardOwnershipGuard ownershipGuard,
        ILogger<Win32ClipboardListener> logger)
    {
        _ownershipGuard = ownershipGuard;
        _logger = logger;
    }

    public IntPtr ListenerHwnd => _hwnd;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
        if (_source is null)
        {
            _logger.LogError("Failed to get HwndSource for clipboard listener window {Hwnd}", hwnd);
            return;
        }

        _source.AddHook(WndProc);
        if (!User32.AddClipboardFormatListener(hwnd))
        {
            _logger.LogError("AddClipboardFormatListener failed with Win32 error {Error}", Marshal.GetLastWin32Error());
        }
        else
        {
            _logger.LogInformation("Clipboard listener registered on HWND {Hwnd}", hwnd);
        }

        _debounceTimer = new DispatcherTimer(DispatcherPriority.Normal, _source.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            RaiseClipboardChanged();
        };

        _lastSequence = User32.GetClipboardSequenceNumber();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_CLIPBOARDUPDATE)
        {
            if (_ownershipGuard.IsOwnWrite)
            {
                return IntPtr.Zero;
            }

            var sequence = User32.GetClipboardSequenceNumber();
            if (sequence == _lastSequence)
            {
                return IntPtr.Zero;
            }

            _lastSequence = sequence;
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        return IntPtr.Zero;
    }

    private void RaiseClipboardChanged()
    {
        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
        {
            SequenceNumber = User32.GetClipboardSequenceNumber()
        });
    }

    public ValueTask DisposeAsync()
    {
        if (_hwnd != IntPtr.Zero)
        {
            User32.RemoveClipboardFormatListener(_hwnd);
        }

        _source?.RemoveHook(WndProc);
        if (_debounceTimer is not null)
        {
            _debounceTimer.Stop();
        }

        return ValueTask.CompletedTask;
    }
}
