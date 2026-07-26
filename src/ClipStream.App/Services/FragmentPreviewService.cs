using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipStream.Core.Models;
using ClipStream.Core.Storage;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.App.Services;

public sealed record FragmentPreviewUiState(
    string PreviewText,
    ImageSource? PreviewImage,
    bool CanOpenInEditor,
    IReadOnlyList<FilePreviewItem>? PreviewFiles = null);

public interface IFragmentPreviewService
{
    Task<FragmentPreviewUiState> LoadAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the fragment image payload to a temp file with a real image extension
    /// (.png / .bmp) so the OS shell association can open it.
    /// </summary>
    Task<string?> ExportImageToTempFileAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default);
}

public sealed class FragmentPreviewService : IFragmentPreviewService
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IBlobStore _blobStore;

    public FragmentPreviewService(IPluginLoader pluginLoader, IBlobStore blobStore)
    {
        _pluginLoader = pluginLoader;
        _blobStore = blobStore;
    }

    public async Task<FragmentPreviewUiState> LoadAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default)
    {
        var context = new FragmentPreviewContext(_blobStore);
        foreach (var plugin in _pluginLoader.PreviewPlugins.OrderBy(p => p.Descriptor.Priority))
        {
            if (!plugin.CanPreview(fragment))
            {
                continue;
            }

            var result = await plugin.BuildPreviewAsync(fragment, context, cancellationToken);
            if (result is null)
            {
                continue;
            }

            return result switch
            {
                TextFragmentPreview text => new FragmentPreviewUiState(
                    text.Text,
                    PreviewImage: null,
                    text.CanOpenInEditor && !string.IsNullOrWhiteSpace(text.Text)),
                ImageFragmentPreview image => CreateImageUiState(image, fragment),
                FilesFragmentPreview files => CreateFilesUiState(files),
                FragmentPreviewResult unknown => throw new InvalidOperationException(
                    $"Unknown preview result type: {unknown.GetType().FullName}")
            };
        }

        return new FragmentPreviewUiState(
            fragment.PreviewText ?? string.Empty,
            PreviewImage: null,
            CanOpenInEditor: false);
    }

    private static FragmentPreviewUiState CreateImageUiState(
        ImageFragmentPreview image,
        ClipboardFragment fragment)
    {
        var bitmap = TryDecodeImage(image);
        if (bitmap is not null)
        {
            return new FragmentPreviewUiState(
                PreviewText: string.Empty,
                bitmap,
                CanOpenInEditor: false);
        }

        // Decode failed — keep fallback text instead of an empty "Нет превью".
        return new FragmentPreviewUiState(
            fragment.PreviewText ?? string.Empty,
            PreviewImage: null,
            CanOpenInEditor: false);
    }

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".msi", ".ps1", ".vbs", ".scr", ".msc"
    };

    private static FragmentPreviewUiState CreateFilesUiState(FilesFragmentPreview files)
    {
        var items = files.Paths
            .Select(CreateFilePreviewItem)
            .ToList();

        return new FragmentPreviewUiState(
            PreviewText: string.Empty,
            PreviewImage: null,
            CanOpenInEditor: false,
            PreviewFiles: items);
    }

    private static FilePreviewItem CreateFilePreviewItem(string path)
    {
        var isDirectory = Directory.Exists(path);
        var exists = isDirectory || File.Exists(path);
        var isExecutable = !isDirectory
            && ExecutableExtensions.Contains(Path.GetExtension(path));
        var displayName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(displayName))
        {
            displayName = path;
        }

        return new FilePreviewItem(
            path,
            displayName,
            isExecutable ? "Запустить" : "Открыть",
            isExecutable,
            exists,
            ShellFileIconLoader.GetIcon(path, exists, isDirectory));
    }

    public async Task<string?> ExportImageToTempFileAsync(
        ClipboardFragment fragment,
        CancellationToken cancellationToken = default)
    {
        var payload = fragment.Payloads.FirstOrDefault(p => p.FormatName is "PNG" or "image/png")
            ?? fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "CF_DIB" or "CF_DIBV5" or "Bitmap");
        if (payload is null)
        {
            return null;
        }

        var data = await _blobStore.GetAsync(payload.StorageKey, cancellationToken);
        if (data is null || data.Length == 0)
        {
            return null;
        }

        var directory = Path.Combine(Path.GetTempPath(), "ClipStream", "preview");
        Directory.CreateDirectory(directory);

        string extension;
        byte[] fileBytes;
        try
        {
            (extension, fileBytes) = CreateImageFileBytes(payload.FormatName, data);
        }
        catch
        {
            return null;
        }

        var path = Path.Combine(directory, $"{fragment.Id:N}{extension}");
        await File.WriteAllBytesAsync(path, fileBytes, cancellationToken);
        return path;
    }

    private static (string Extension, byte[] Bytes) CreateImageFileBytes(string formatName, byte[] data)
    {
        if (formatName is "PNG" or "image/png")
        {
            return (".png", data);
        }

        if (formatName is "CF_DIB" or "CF_DIBV5")
        {
            return (".bmp", WrapDibAsBmpFile(data));
        }

        // "Bitmap" or unknown: encoded image, or raw DIB without a file header.
        if (data.Length >= 8
            && data[0] == 0x89
            && data[1] == (byte)'P'
            && data[2] == (byte)'N'
            && data[3] == (byte)'G')
        {
            return (".png", data);
        }

        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M')
        {
            return (".bmp", data);
        }

        return (".bmp", WrapDibAsBmpFile(data));
    }

    private static ImageSource? TryDecodeImage(ImageFragmentPreview preview)
    {
        try
        {
            var bitmap = preview.FormatName is "CF_DIB" or "CF_DIBV5"
                ? CreateBitmapFromDib(preview.Data)
                : CreateBitmapFromEncoded(preview.Data);
            return bitmap;
        }
        catch
        {
            // Clipboard "Bitmap" / mislabeled payloads may still be raw DIB bytes.
            if (preview.FormatName is not ("CF_DIB" or "CF_DIBV5"))
            {
                try
                {
                    return CreateBitmapFromDib(preview.Data);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }

    private static BitmapSource CreateBitmapFromEncoded(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        if (frame.CanFreeze)
        {
            frame.Freeze();
        }

        return frame;
    }

    private static BitmapSource CreateBitmapFromDib(byte[] dib) =>
        CreateBitmapFromEncoded(WrapDibAsBmpFile(dib));

    /// <summary>
    /// Wraps CF_DIB / CF_DIBV5 payload in a BMP file header so decoders and shell associations work.
    /// </summary>
    private static byte[] WrapDibAsBmpFile(byte[] dib)
    {
        if (dib.Length < 40)
        {
            throw new InvalidOperationException("Invalid DIB data.");
        }

        var headerSize = BitConverter.ToInt32(dib, 0);
        if (headerSize < 40 || headerSize > dib.Length)
        {
            throw new InvalidOperationException("Invalid DIB header size.");
        }

        var bitCount = BitConverter.ToUInt16(dib, 14);
        var compression = BitConverter.ToUInt32(dib, 16);
        var colorsUsed = BitConverter.ToUInt32(dib, 32);

        var colorTableBytes = 0;
        if (bitCount <= 8)
        {
            var entries = colorsUsed != 0 ? (int)colorsUsed : 1 << bitCount;
            colorTableBytes = entries * 4;
        }
        else if (compression == 3 /* BI_BITFIELDS */ && headerSize == 40)
        {
            // BITMAPINFOHEADER + 3 color masks (V4/V5 headers already include masks).
            colorTableBytes = 12;
        }

        var pixelDataOffset = 14 + headerSize + colorTableBytes;
        if (pixelDataOffset > 14 + dib.Length)
        {
            throw new InvalidOperationException("Invalid DIB pixel offset.");
        }

        var fileSize = 14 + dib.Length;
        var bmp = new byte[fileSize];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.TryWriteBytes(bmp.AsSpan(2), fileSize);
        BitConverter.TryWriteBytes(bmp.AsSpan(10), pixelDataOffset);
        Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);
        return bmp;
    }
}
