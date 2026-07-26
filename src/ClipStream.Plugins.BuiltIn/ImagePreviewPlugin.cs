using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class ImagePreviewPlugin : BuiltInPluginBase, IFragmentPreviewPlugin
{
    public override PluginDescriptor Descriptor { get; } =
        new("builtin.preview.image", "Image preview", "1.0.0", 20);

    public bool CanPreview(ClipboardFragment fragment) =>
        fragment.Kind == FragmentKind.Image;

    public async Task<FragmentPreviewResult?> BuildPreviewAsync(
        ClipboardFragment fragment,
        FragmentPreviewContext context,
        CancellationToken cancellationToken = default)
    {
        // Prefer encoded PNG when present — DIB bit-depth varies and is harder to decode.
        var payload = fragment.Payloads.FirstOrDefault(p => p.FormatName is "PNG" or "image/png")
            ?? fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "CF_DIB" or "CF_DIBV5" or "Bitmap");
        if (payload is null)
        {
            return null;
        }

        var data = await context.BlobStore.GetAsync(payload.StorageKey, cancellationToken);
        if (data is null || data.Length == 0)
        {
            return null;
        }

        return new ImageFragmentPreview(data, payload.FormatName);
    }
}
