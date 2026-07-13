using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ClipStream.Clipboard.Listener;
using ClipStream.Clipboard.Win32;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipStream.Clipboard.Capture;

public interface IClipboardCaptureService
{
    Task<RawClipboardCapture?> CaptureAsync(CancellationToken cancellationToken = default);
}

public sealed class ClipboardCaptureService : IClipboardCaptureService
{
    private static readonly Dictionary<uint, string> KnownFormats = new()
    {
        [1] = "CF_TEXT",
        [2] = "CF_BITMAP",
        [3] = "CF_METAFILEPICT",
        [8] = "CF_DIB",
        [13] = "CF_UNICODETEXT",
        [15] = "CF_HDROP",
        [16] = "CF_LOCALE",
        [17] = "CF_DIBV5"
    };

    private readonly IClipboardListener _listener;
    private readonly ILogger<ClipboardCaptureService> _logger;

    public ClipboardCaptureService(IClipboardListener listener, ILogger<ClipboardCaptureService> logger)
    {
        _listener = listener;
        _logger = logger;
    }

    public Task<RawClipboardCapture?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Capture());
    }

    private RawClipboardCapture? Capture()
    {
        var owner = _listener.ListenerHwnd;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (User32.OpenClipboard(owner))
            {
                try
                {
                    var capture = CaptureInternal();
                    if (capture.Formats.Count > 0)
                    {
                        return capture;
                    }
                }
                finally
                {
                    User32.CloseClipboard();
                }
            }
            else
            {
                _logger.LogDebug("OpenClipboard attempt {Attempt} failed with error {Error}", attempt + 1, Marshal.GetLastWin32Error());
            }

            Thread.Sleep(15 * (attempt + 1));
        }

        _logger.LogWarning("Failed to capture clipboard after retries");
        return null;
    }

    private static RawClipboardCapture CaptureInternal()
    {
        var formats = new List<RawFormatData>();
        uint format = 0;
        while ((format = User32.EnumClipboardFormats(format)) != 0)
        {
            if (format is 2 or 3)
            {
                continue;
            }

            var data = ReadFormatData(format);
            if (data is not null)
            {
                formats.Add(new RawFormatData(GetFormatName(format), data));
            }
        }

        GetSourceProcess(out var processName, out var processId);
        return new RawClipboardCapture(
            DateTimeOffset.Now,
            User32.GetClipboardSequenceNumber(),
            processName,
            processId,
            formats);
    }

    private static byte[]? ReadFormatData(uint format)
    {
        var handle = User32.GetClipboardData(format);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var pointer = User32.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var size = (int)User32.GlobalSize(handle).ToUInt64();
            if (size <= 0)
            {
                return [];
            }

            var data = new byte[size];
            Marshal.Copy(pointer, data, 0, size);
            return data;
        }
        finally
        {
            User32.GlobalUnlock(handle);
        }
    }

    private static string GetFormatName(uint format)
    {
        if (KnownFormats.TryGetValue(format, out var known))
        {
            return known;
        }

        var sb = new StringBuilder(256);
        var length = GetClipboardFormatName(format, sb, sb.Capacity);
        return length > 0 ? sb.ToString() : $"Format_{format}";
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClipboardFormatName(uint format, StringBuilder buffer, int size);

    private static void GetSourceProcess(out string? processName, out int? processId)
    {
        processName = null;
        processId = null;

        try
        {
            var hwnd = User32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            User32.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
            {
                return;
            }

            processId = (int)pid;
            var processHandle = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, (int)pid);
            if (processHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var sb = new StringBuilder(1024);
                var size = sb.Capacity;
                if (Kernel32.QueryFullProcessImageName(processHandle, 0, sb, ref size))
                {
                    processName = Path.GetFileName(sb.ToString());
                }
            }
            finally
            {
                Kernel32.CloseHandle(processHandle);
            }
        }
        catch
        {
            // Source process metadata is optional; capture must continue.
        }
    }
}
