using ClipStream.Core.Export;
using ClipStream.Core.Models;

namespace ClipStream.Export;

public sealed class ExportPathBuilder
{
    public string BuildFileName(MarkdownExportOptions options, ClipboardFragment fragment) =>
        options.FilenameStrategy switch
        {
            FilenameStrategy.Guid => $"{fragment.Id:N}.md",
            FilenameStrategy.SlugFromPreview => $"{SlugGenerator.FromText(fragment.PreviewText)}.md",
            FilenameStrategy.SlugFromTitle => $"{SlugGenerator.FromText(fragment.Title)}.md",
            _ => $"{fragment.CapturedAt:HHmmss}-{SlugGenerator.FromText(fragment.PreviewText)}.md"
        };

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
