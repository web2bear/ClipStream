using ClipStream.Core;
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class FilesPreviewPlugin : BuiltInPluginBase, IFragmentPreviewPlugin
{
    public override PluginDescriptor Descriptor { get; } =
        new("builtin.preview.files", "Files preview", "1.0.0", 30);

    public bool CanPreview(ClipboardFragment fragment) =>
        fragment.Kind == FragmentKind.Files;

    public async Task<FragmentPreviewResult?> BuildPreviewAsync(
        ClipboardFragment fragment,
        FragmentPreviewContext context,
        CancellationToken cancellationToken = default)
    {
        var paths = await ResolvePathsAsync(fragment, context, cancellationToken);
        if (paths.Count == 0)
        {
            return null;
        }

        return new FilesFragmentPreview(paths);
    }

    private static async Task<IReadOnlyList<string>> ResolvePathsAsync(
        ClipboardFragment fragment,
        FragmentPreviewContext context,
        CancellationToken cancellationToken)
    {
        var payload = fragment.Payloads.FirstOrDefault(p =>
            p.FormatName is "FileDrop" or "FileNameW" or "CF_HDROP");
        if (payload is not null)
        {
            var data = await context.BlobStore.GetAsync(payload.StorageKey, cancellationToken);
            if (data is { Length: > 0 })
            {
                var fromBlob = FileDropParser.Parse(data);
                if (fromBlob.Count > 0)
                {
                    return fromBlob;
                }
            }
        }

        return ParsePreviewText(fragment.PreviewText);
    }

    private static IReadOnlyList<string> ParsePreviewText(string? previewText)
    {
        if (string.IsNullOrWhiteSpace(previewText) || previewText == "[Files]")
        {
            return [];
        }

        return previewText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsLikelyPath)
            .ToList();
    }

    private static bool IsLikelyPath(string value) =>
        value.Length > 0
        && value != "[Files]"
        && !value.Contains('\0')
        && (value.Contains('\\') || value.Contains('/') || Path.IsPathRooted(value));
}
