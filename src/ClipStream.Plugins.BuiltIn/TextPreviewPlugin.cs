using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class TextPreviewPlugin : BuiltInPluginBase, IFragmentPreviewPlugin
{
    public override PluginDescriptor Descriptor { get; } =
        new("builtin.preview.text", "Text preview", "1.0.0", 10);

    public bool CanPreview(ClipboardFragment fragment) =>
        fragment.Kind is FragmentKind.Text or FragmentKind.RichText;

    public async Task<FragmentPreviewResult?> BuildPreviewAsync(
        ClipboardFragment fragment,
        FragmentPreviewContext context,
        CancellationToken cancellationToken = default)
    {
        var payload = fragment.Kind switch
        {
            FragmentKind.RichText => fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "HTML Format" or "text/html")
                ?? fragment.Payloads.FirstOrDefault(p =>
                    p.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain" or "Text" or "CF_TEXT"),
            _ => fragment.Payloads.FirstOrDefault(p =>
                p.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain")
                ?? fragment.Payloads.FirstOrDefault(p => p.FormatName is "Text" or "CF_TEXT")
        };

        if (payload is null)
        {
            var fallback = fragment.PreviewText ?? string.Empty;
            return string.IsNullOrWhiteSpace(fallback)
                ? null
                : new TextFragmentPreview(fallback, CanOpenInEditor: true);
        }

        var data = await context.BlobStore.GetAsync(payload.StorageKey, cancellationToken);
        if (data is null || data.Length == 0)
        {
            var fallback = fragment.PreviewText ?? string.Empty;
            return string.IsNullOrWhiteSpace(fallback)
                ? null
                : new TextFragmentPreview(fallback, CanOpenInEditor: true);
        }

        try
        {
            string text;
            if (payload.FormatName is "HTML Format" or "text/html")
            {
                text = System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');
            }
            else if (payload.FormatName.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
                     || payload.FormatName is "text/plain")
            {
                text = System.Text.Encoding.Unicode.GetString(data).TrimEnd('\0');
            }
            else
            {
                text = System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0');
            }

            return new TextFragmentPreview(text, CanOpenInEditor: !string.IsNullOrWhiteSpace(text));
        }
        catch
        {
            var fallback = fragment.PreviewText ?? string.Empty;
            return string.IsNullOrWhiteSpace(fallback)
                ? null
                : new TextFragmentPreview(fallback, CanOpenInEditor: true);
        }
    }
}
