using ClipStream.Core.Export;
using ClipStream.Core.Models;

namespace ClipStream.Export;

public sealed class ExportPathBuilder
{
    public string BuildFragmentPath(
        ObsidianExportOptions options,
        ClipboardFragment fragment,
        ClipStreamEntity? stream,
        string fileName)
    {
        var datePath = $"{fragment.CapturedAt:yyyy}/{fragment.CapturedAt:MM}/{fragment.CapturedAt:dd}";
        return options.Layout switch
        {
            ObsidianLayout.SingleFolder => fileName,
            ObsidianLayout.FlatByDate => Path.Combine(datePath, fileName),
            ObsidianLayout.StreamsAsFolders => Path.Combine(
                "streams",
                SlugGenerator.FromStreamName(stream?.Name ?? "inbox"),
                datePath,
                fileName),
            _ => fileName
        };
    }

    public string BuildStreamIndexPath(ClipStreamEntity stream) =>
        Path.Combine("streams", SlugGenerator.FromStreamName(stream.Name), "_index.md");

    public string BuildFileName(ObsidianExportOptions options, ClipboardFragment fragment)
    {
        return options.FilenameStrategy switch
        {
            FilenameStrategy.Guid => $"{fragment.Id:N}.md",
            FilenameStrategy.SlugFromPreview => $"{SlugGenerator.FromText(fragment.PreviewText)}.md",
            _ => $"{fragment.CapturedAt:HHmmss}-{SlugGenerator.FromText(fragment.PreviewText)}.md"
        };
    }

    public string ResolveUniquePath(string directory, string fileName, HashSet<string> usedNames)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = fileName;
        var index = 2;
        while (usedNames.Contains(candidate.ToLowerInvariant()) || File.Exists(Path.Combine(directory, candidate)))
        {
            candidate = $"{baseName}-{index}{extension}";
            index++;
        }

        usedNames.Add(candidate.ToLowerInvariant());
        return candidate;
    }
}
