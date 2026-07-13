using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class GenericBinaryPlugin : BuiltInPluginBase, IClipboardFormatPlugin
{
    public override PluginDescriptor Descriptor { get; } = new("builtin.generic", "Generic Binary", "1.0.0", 1000);

    public bool CanHandle(RawClipboardCapture capture) => capture.Formats.Count > 0;

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var payloads = new List<FormatPayload>();
        foreach (var format in capture.Formats)
        {
            var key = await context.BlobStore.StoreAsync(format.Data, cancellationToken);
            payloads.Add(new FormatPayload(format.FormatName, key, format.Data.Length, ContentHashHelper.ComputeBlobHash(format.Data)));
        }

        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);
        var preview = $"[Binary: {string.Join(", ", capture.Formats.Select(f => f.FormatName))}]";
        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.Binary,
            preview,
            capture.SourceProcessName,
            capture.SourceProcessId,
            payloads,
            new Dictionary<string, string>(),
            hash);

        return new FragmentProduced(fragment);
    }
}
