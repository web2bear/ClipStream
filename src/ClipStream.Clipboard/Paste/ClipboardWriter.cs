using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using ClipStream.Clipboard.Guard;
using ClipStream.Clipboard.Win32;
using ClipStream.Core.Models;
using ClipStream.Core.Storage;

namespace ClipStream.Clipboard.Paste;

public interface IClipboardPayloadBuilder
{
    Task<IDataObject> BuildDataObjectAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);
}

public interface IClipboardWriter
{
    Task SetFragmentAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);

    Task PasteFragmentToActiveWindowAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);
}

public sealed class ClipboardPayloadBuilder : IClipboardPayloadBuilder
{
    private readonly IBlobStore _blobStore;

    public ClipboardPayloadBuilder(IBlobStore blobStore) => _blobStore = blobStore;

    public async Task<IDataObject> BuildDataObjectAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default)
    {
        var dataObject = new DataObject();
        foreach (var payload in fragment.Payloads)
        {
            var data = await _blobStore.GetAsync(payload.StorageKey, cancellationToken);
            if (data is null)
            {
                continue;
            }

            switch (payload.FormatName)
            {
                case "UnicodeText" or "CF_UNICODETEXT":
                    dataObject.SetData(DataFormats.UnicodeText, System.Text.Encoding.Unicode.GetString(data).TrimEnd('\0'));
                    break;
                case "Text" or "CF_TEXT":
                    dataObject.SetData(DataFormats.Text, System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0'));
                    break;
                case "HTML Format" or "text/html":
                    dataObject.SetData(DataFormats.Html, System.Text.Encoding.UTF8.GetString(data));
                    break;
                case "FileDrop" or "CF_HDROP":
                    SetFileDrop(dataObject, data);
                    break;
                case "CF_DIB" or "PNG" or "Bitmap":
                    TrySetImage(dataObject, data, payload.FormatName);
                    break;
                default:
                    dataObject.SetData(payload.FormatName, data);
                    break;
            }
        }

        if (fragment.PreviewText is not null && !dataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            dataObject.SetData(DataFormats.UnicodeText, fragment.PreviewText);
        }

        return dataObject;
    }

    private static void SetFileDrop(IDataObject dataObject, byte[] data)
    {
        var paths = new List<string>();
        if (data.Length >= 20)
        {
            var offset = 20;
            while (offset < data.Length - 1)
            {
                var end = Array.IndexOf(data, (byte)0, offset);
                if (end < 0)
                {
                    break;
                }

                if (end > offset)
                {
                    paths.Add(System.Text.Encoding.Unicode.GetString(data, offset, end - offset));
                }

                offset = end + 2;
            }
        }

        if (paths.Count > 0)
        {
            dataObject.SetData(DataFormats.FileDrop, paths.ToArray());
        }
    }

    private static void TrySetImage(IDataObject dataObject, byte[] data, string formatName)
    {
        try
        {
            if (formatName is "CF_DIB")
            {
                dataObject.SetData(DataFormats.Bitmap, CreateBitmapFromDib(data));
                return;
            }

            using var stream = new MemoryStream(data);
            if (formatName is "PNG" or "Bitmap")
            {
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    dataObject.SetData(DataFormats.Bitmap, decoder.Frames[0]);
                }
            }
        }
        catch
        {
            dataObject.SetData(formatName, data);
        }
    }

    private static System.Windows.Media.Imaging.BitmapSource CreateBitmapFromDib(byte[] dib)
    {
        if (dib.Length < 40)
        {
            throw new InvalidOperationException("Invalid DIB data.");
        }

        var height = BitConverter.ToInt32(dib, 8);
        var topDown = height < 0;
        if (topDown)
        {
            height = -height;
        }

        var stride = ((BitConverter.ToInt16(dib, 10) * BitConverter.ToInt16(dib, 14) + 31) / 32) * 4;
        var pixelOffset = BitConverter.ToInt32(dib, 0);
        if (pixelOffset <= 0 || pixelOffset >= dib.Length)
        {
            pixelOffset = 40;
        }

        var pixelData = new byte[stride * height];
        var available = Math.Min(pixelData.Length, dib.Length - pixelOffset);
        Array.Copy(dib, pixelOffset, pixelData, 0, available);

        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
            BitConverter.ToInt32(dib, 4),
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgr24,
            null,
            pixelData,
            stride);

        if (topDown)
        {
            return bitmap;
        }

        var flipped = new byte[pixelData.Length];
        for (var row = 0; row < height; row++)
        {
            Array.Copy(pixelData, row * stride, flipped, (height - row - 1) * stride, stride);
        }

        return System.Windows.Media.Imaging.BitmapSource.Create(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bitmap.DpiX,
            bitmap.DpiY,
            bitmap.Format,
            bitmap.Palette,
            flipped,
            stride);
    }
}

public sealed class ClipboardWriter : IClipboardWriter
{
    private static readonly TimeSpan ActivationDelay = TimeSpan.FromMilliseconds(75);

    private readonly IClipboardPayloadBuilder _payloadBuilder;
    private readonly IClipboardOwnershipGuard _ownershipGuard;
    private readonly IForegroundWindowTracker _foregroundTracker;

    public ClipboardWriter(
        IClipboardPayloadBuilder payloadBuilder,
        IClipboardOwnershipGuard ownershipGuard,
        IForegroundWindowTracker foregroundTracker)
    {
        _payloadBuilder = payloadBuilder;
        _ownershipGuard = ownershipGuard;
        _foregroundTracker = foregroundTracker;
    }

    public async Task SetFragmentAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default)
    {
        var dataObject = await _payloadBuilder.BuildDataObjectAsync(fragment, cancellationToken);
        await global::System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            using (_ownershipGuard.SuppressListening())
            {
                global::System.Windows.Clipboard.SetDataObject(dataObject, copy: true);
            }
        });
    }

    public async Task PasteFragmentToActiveWindowAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default)
    {
        var targetHwnd = _foregroundTracker.LastExternalForeground;
        if (!WindowActivator.IsValidWindow(targetHwnd))
        {
            throw new InvalidOperationException("No target window found. Focus the destination app before pasting.");
        }

        await SetFragmentAsync(fragment, cancellationToken);

        await global::System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (global::System.Windows.Application.Current.MainWindow is { IsVisible: true } mainWindow)
            {
                mainWindow.Hide();
            }
        });

        await Task.Delay(ActivationDelay, cancellationToken);

        if (!WindowActivator.TryActivate(targetHwnd))
        {
            throw new InvalidOperationException("Could not activate the target window.");
        }

        await Task.Delay(ActivationDelay, cancellationToken);
        KeyboardInput.SendPasteShortcut();
    }
}
