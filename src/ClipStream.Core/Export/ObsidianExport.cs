namespace ClipStream.Core.Export;

public enum ObsidianLayout
{
    StreamsAsFolders,
    FlatByDate,
    SingleFolder
}

public enum FilenameStrategy
{
    TimestampAndSlug,
    Guid,
    SlugFromPreview
}

public sealed record ObsidianExportOptions
{
    public required string TargetDirectory { get; init; }

    public ObsidianLayout Layout { get; init; } = ObsidianLayout.StreamsAsFolders;

    public bool IncludeAttachments { get; init; } = true;

    public bool IncludeRawFormats { get; init; }

    public FilenameStrategy FilenameStrategy { get; init; } = FilenameStrategy.TimestampAndSlug;

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
    IReadOnlyList<ExportItem> Items,
    string? ManifestPath);

public sealed record ExportProgress(int Current, int Total, string? CurrentFile);

public interface IObsidianVaultExporter
{
    Task<ExportResult> ExportFragmentAsync(
        Guid fragmentId,
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ExportResult> ExportStreamAsync(
        Guid streamId,
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ExportResult> ExportAllAsync(
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
