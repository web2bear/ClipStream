using System.Text;
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class HtmlFormatPlugin : BuiltInPluginBase, IClipboardFormatPlugin
{
    public override PluginDescriptor Descriptor { get; } = new("builtin.html", "HTML", "1.0.0", 20);

    public bool CanHandle(RawClipboardCapture capture) =>
        capture.Formats.Any(f => f.FormatName is "HTML Format" or "text/html");

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var htmlFormat = capture.Formats.FirstOrDefault(f => f.FormatName is "HTML Format" or "text/html");
        if (htmlFormat is null)
        {
            return new Skipped("No HTML format");
        }

        var textFormat = capture.Formats.FirstOrDefault(f => f.FormatName is "UnicodeText" or "Text");
        string? preview = null;
        if (textFormat is not null)
        {
            preview = textFormat.FormatName.Contains("Unicode", StringComparison.OrdinalIgnoreCase)
                ? Encoding.Unicode.GetString(textFormat.Data).TrimEnd('\0')
                : Encoding.UTF8.GetString(textFormat.Data).TrimEnd('\0');
        }

        preview ??= ExtractTextFromHtml(htmlFormat.Data);

        var payloads = new List<FormatPayload>();
        foreach (var format in capture.Formats.Where(f =>
            f.FormatName is "HTML Format" or "text/html" or "UnicodeText" or "Text"))
        {
            var key = await context.BlobStore.StoreAsync(format.Data, cancellationToken);
            payloads.Add(new FormatPayload(format.FormatName, key, format.Data.Length, ContentHashHelper.ComputeBlobHash(format.Data)));
        }

        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);
        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.RichText,
            preview?.Length > 500 ? preview[..500] : preview,
            capture.SourceProcessName,
            capture.SourceProcessId,
            payloads,
            new Dictionary<string, string> { ["hasHtml"] = "true" },
            hash);

        return new FragmentProduced(fragment);
    }

    private static string ExtractTextFromHtml(byte[] data)
    {
        var html = Encoding.UTF8.GetString(data);
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }
}
