using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public abstract class BuiltInPluginBase : IClipStreamPlugin
{
    public abstract PluginDescriptor Descriptor { get; }
}

public sealed class TextFormatPlugin : BuiltInPluginBase, IClipboardFormatPlugin
{
    public override PluginDescriptor Descriptor { get; } = new("builtin.text", "Text", "1.0.0", 10);

    public bool CanHandle(RawClipboardCapture capture) =>
        capture.Formats.Any(f => f.FormatName is
            "UnicodeText" or "Text" or "CF_UNICODETEXT" or "CF_TEXT" or "text/plain");

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var textFormat = capture.Formats.FirstOrDefault(f => f.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain")
            ?? capture.Formats.FirstOrDefault(f => f.FormatName is "Text" or "CF_TEXT");
        if (textFormat is null)
        {
            return new Skipped("No text format");
        }

        var text = textFormat.FormatName.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
            ? System.Text.Encoding.Unicode.GetString(textFormat.Data).TrimEnd('\0')
            : System.Text.Encoding.UTF8.GetString(textFormat.Data).TrimEnd('\0');

        var storageKey = await context.BlobStore.StoreAsync(textFormat.Data, cancellationToken);
        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);
        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.Text,
            text.Length > 500 ? text[..500] : text,
            capture.SourceProcessName,
            capture.SourceProcessId,
            [new FormatPayload(textFormat.FormatName, storageKey, textFormat.Data.Length, ContentHashHelper.ComputeBlobHash(textFormat.Data))],
            new Dictionary<string, string>(),
            hash);

        return new FragmentProduced(fragment);
    }
}
