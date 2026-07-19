using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipStream.Core.Models;
using ClipStream.Core.Storage;

namespace ClipStream.App.Services;

public interface IFragmentPreviewService
{
    Task<ImageSource?> TryLoadImageAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);

    Task<string?> TryLoadTextAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);
}

public sealed class FragmentPreviewService : IFragmentPreviewService
{
    private readonly IBlobStore _blobStore;

    public FragmentPreviewService(IBlobStore blobStore) => _blobStore = blobStore;

    public async Task<ImageSource?> TryLoadImageAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default)
    {
        if (fragment.Kind != FragmentKind.Image)
        {
            return null;
        }

        var payload = fragment.Payloads.FirstOrDefault(p =>
            p.FormatName is "CF_DIB" or "PNG" or "Bitmap" or "image/png");
        if (payload is null)
        {
            return null;
        }

        var data = await _blobStore.GetAsync(payload.StorageKey, cancellationToken);
        if (data is null || data.Length == 0)
        {
            return null;
        }

        try
        {
            return payload.FormatName is "CF_DIB"
                ? CreateBitmapFromDib(data)
                : CreateBitmapFromEncoded(data);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> TryLoadTextAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default)
    {
        if (fragment.Kind is not (FragmentKind.Text or FragmentKind.RichText or FragmentKind.Files))
        {
            return fragment.PreviewText;
        }

        var payload = fragment.Kind switch
        {
            FragmentKind.RichText => fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "HTML Format" or "text/html")
                ?? fragment.Payloads.FirstOrDefault(p =>
                    p.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain" or "Text" or "CF_TEXT"),
            FragmentKind.Files => fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "FileDrop" or "FileNameW" or "CF_HDROP"),
            _ => fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain")
                ?? fragment.Payloads.FirstOrDefault(p => p.FormatName is "Text" or "CF_TEXT")
        };

        if (payload is null)
        {
            return fragment.PreviewText;
        }

        var data = await _blobStore.GetAsync(payload.StorageKey, cancellationToken);
        if (data is null || data.Length == 0)
        {
            return fragment.PreviewText;
        }

        try
        {
            if (fragment.Kind == FragmentKind.Files)
            {
                return fragment.PreviewText;
            }

            if (payload.FormatName is "HTML Format" or "text/html")
            {
                return System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');
            }

            if (payload.FormatName.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
                || payload.FormatName is "text/plain")
            {
                return System.Text.Encoding.Unicode.GetString(data).TrimEnd('\0');
            }

            return System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');
        }
        catch
        {
            return fragment.PreviewText;
        }
    }

    private static BitmapSource CreateBitmapFromEncoded(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    private static BitmapSource CreateBitmapFromDib(byte[] dib)
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

        if (!topDown)
        {
            var flipped = new byte[pixelData.Length];
            for (var row = 0; row < height; row++)
            {
                Array.Copy(pixelData, row * stride, flipped, (height - row - 1) * stride, stride);
            }

            pixelData = flipped;
        }

        var bitmap = BitmapSource.Create(
            BitConverter.ToInt32(dib, 4),
            height,
            96,
            96,
            PixelFormats.Bgr24,
            null,
            pixelData,
            stride);
        bitmap.Freeze();
        return bitmap;
    }
}
