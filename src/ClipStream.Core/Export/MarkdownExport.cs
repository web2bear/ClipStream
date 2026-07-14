namespace ClipStream.Core.Export;

public enum FilenameStrategy
{
    TimestampAndSlug,
    Guid,
    SlugFromPreview,
    SlugFromTitle
}

public sealed record MarkdownExportOptions
{
    public required string TargetDirectory { get; init; }

    public bool IncludeAttachments { get; init; } = true;

    public FilenameStrategy FilenameStrategy { get; init; } = FilenameStrategy.SlugFromTitle;

    public bool OverwriteExisting { get; init; }

    public string? TagPrefix { get; init; } = "clipstream";
}

public sealed record ExportItem(
    Guid FragmentId,
    string RelativePath,
    string? AttachmentFolder);

public sealed record ExportResult(
    int FilesWritten,
    int AttachmentsCopied,
    int Skipped,
    IReadOnlyList<ExportItem> Items);

public sealed record ExportProgress(int Current, int Total, string? CurrentFile);

public interface IMarkdownExporter
{
    Task<ExportResult> ExportFragmentAsync(
        Guid fragmentId,
        MarkdownExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ExportResult> ExportStreamAsync(
        Guid streamId,
        MarkdownExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
