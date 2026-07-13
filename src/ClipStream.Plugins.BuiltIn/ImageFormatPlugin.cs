using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class ImageFormatPlugin : BuiltInPluginBase, IClipboardFormatPlugin
{
    public override PluginDescriptor Descriptor { get; } = new("builtin.image", "Image", "1.0.0", 30);

    public bool CanHandle(RawClipboardCapture capture) =>
        capture.Formats.Any(f => f.FormatName is "CF_DIB" or "PNG" or "Bitmap" or "image/png");

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var imageFormat = capture.Formats.FirstOrDefault(f => f.FormatName is "CF_DIB" or "PNG" or "Bitmap" or "image/png");
        if (imageFormat is null)
        {
            return new Skipped("No image format");
        }

        var storageKey = await context.BlobStore.StoreAsync(imageFormat.Data, cancellationToken);
        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);
        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.Image,
            "[Image]",
            capture.SourceProcessName,
            capture.SourceProcessId,
            [new FormatPayload(imageFormat.FormatName, storageKey, imageFormat.Data.Length, ContentHashHelper.ComputeBlobHash(imageFormat.Data))],
            new Dictionary<string, string>(),
            hash);

        return new FragmentProduced(fragment);
    }
}
