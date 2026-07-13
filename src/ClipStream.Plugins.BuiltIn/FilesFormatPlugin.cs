using System.Text;
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class FilesFormatPlugin : BuiltInPluginBase, IClipboardFormatPlugin
{
    public override PluginDescriptor Descriptor { get; } = new("builtin.files", "Files", "1.0.0", 40);

    public bool CanHandle(RawClipboardCapture capture) =>
        capture.Formats.Any(f => f.FormatName is "FileDrop" or "FileNameW" or "CF_HDROP");

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var filesFormat = capture.Formats.FirstOrDefault(f => f.FormatName is "FileDrop" or "FileNameW" or "CF_HDROP");
        if (filesFormat is null)
        {
            return new Skipped("No files format");
        }

        var paths = ParseFileDrop(filesFormat.Data);
        var preview = paths.Count == 0 ? "[Files]" : string.Join(Environment.NewLine, paths);
        var storageKey = await context.BlobStore.StoreAsync(filesFormat.Data, cancellationToken);
        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);

        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.Files,
            preview,
            capture.SourceProcessName,
            capture.SourceProcessId,
            [new FormatPayload(filesFormat.FormatName, storageKey, filesFormat.Data.Length, ContentHashHelper.ComputeBlobHash(filesFormat.Data))],
            new Dictionary<string, string> { ["fileCount"] = paths.Count.ToString() },
            hash);

        return new FragmentProduced(fragment);
    }

    private static List<string> ParseFileDrop(byte[] data)
    {
        if (data.Length < 20)
        {
            return [];
        }

        var paths = new List<string>();
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
                paths.Add(Encoding.Unicode.GetString(data, offset, end - offset));
            }

            offset = end + 2;
            if (offset < data.Length && data[offset] == 0 && data[offset - 1] == 0)
            {
                break;
            }
        }

        return paths;
    }
}
