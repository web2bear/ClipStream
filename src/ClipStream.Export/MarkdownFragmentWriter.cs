using System.Text;
using ClipStream.Core.Export;
using ClipStream.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ClipStream.Export;

public sealed class MarkdownFragmentWriter
{
    private readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public async Task WriteAsync(
        string filePath,
        ClipboardFragment fragment,
        ClipStreamEntity? stream,
        IReadOnlyList<string> attachmentPaths,
        MarkdownExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var tagPrefix = options.TagPrefix ?? "clipstream";
        var frontmatter = new Dictionary<string, object?>
        {
            ["id"] = fragment.Id.ToString(),
            ["title"] = fragment.Title,
            ["created"] = fragment.CapturedAt.ToString("O"),
            ["tags"] = new List<string>
            {
                tagPrefix,
                $"{tagPrefix}/{fragment.Kind.ToString().ToLowerInvariant()}"
            },
            ["source"] = fragment.SourceProcessName,
            ["stream"] = stream?.Name ?? "inbox",
            ["kind"] = fragment.Kind.ToString(),
            ["contentHash"] = fragment.ContentHash,
            ["formats"] = fragment.Payloads.Select(p => p.FormatName).ToList(),
            ["clipstream"] = new Dictionary<string, object?>
            {
                ["exportedAt"] = DateTimeOffset.UtcNow.ToString("O"),
                ["version"] = 1
            }
        };

        if (attachmentPaths.Count > 0)
        {
            frontmatter["attachments"] = attachmentPaths;
        }

        var yaml = _serializer.Serialize(frontmatter);
        var body = BuildBody(fragment, attachmentPaths);
        var content = $"---{Environment.NewLine}{yaml}---{Environment.NewLine}{Environment.NewLine}{body}";
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
    }

    private static string BuildBody(ClipboardFragment fragment, IReadOnlyList<string> attachmentPaths)
    {
        if (fragment.Kind == FragmentKind.Image && attachmentPaths.Count > 0)
        {
            return string.Join(Environment.NewLine, attachmentPaths.Select(path => $"![[{path}]]"));
        }

        if (fragment.Kind == FragmentKind.Files && attachmentPaths.Count > 0)
        {
            var lines = attachmentPaths.Select(path => $"- [[{path}]]").ToList();
            if (!string.IsNullOrWhiteSpace(fragment.PreviewText))
            {
                lines.Insert(0, fragment.PreviewText);
            }

            return string.Join(Environment.NewLine, lines);
        }

        return fragment.PreviewText ?? string.Empty;
    }
}
