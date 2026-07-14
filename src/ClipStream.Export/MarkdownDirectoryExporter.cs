using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace ClipStream.Export;

public sealed class MarkdownDirectoryExporter : IMarkdownExporter
{
    private readonly IFragmentRepository _fragmentRepository;
    private readonly IStreamRepository _streamRepository;
    private readonly ExportPathBuilder _pathBuilder;
    private readonly MarkdownFragmentWriter _markdownWriter;
    private readonly AttachmentCopier _attachmentCopier;
    private readonly ILogger<MarkdownDirectoryExporter> _logger;

    public MarkdownDirectoryExporter(
        IFragmentRepository fragmentRepository,
        IStreamRepository streamRepository,
        ExportPathBuilder pathBuilder,
        MarkdownFragmentWriter markdownWriter,
        AttachmentCopier attachmentCopier,
        ILogger<MarkdownDirectoryExporter> logger)
    {
        _fragmentRepository = fragmentRepository;
        _streamRepository = streamRepository;
        _pathBuilder = pathBuilder;
        _markdownWriter = markdownWriter;
        _attachmentCopier = attachmentCopier;
        _logger = logger;
    }

    public Task<ExportResult> ExportFragmentAsync(
        Guid fragmentId,
        MarkdownExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        ExportInternalAsync([fragmentId], options, progress, cancellationToken);

    public async Task<ExportResult> ExportStreamAsync(
        Guid streamId,
        MarkdownExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stream = await _streamRepository.GetByIdAsync(streamId, cancellationToken);
        if (stream is null)
        {
            return new ExportResult(0, 0, 0, []);
        }

        var fragments = await _fragmentRepository.GetByStreamAsync(streamId, 0, int.MaxValue, cancellationToken);
        var ids = fragments.Select(fragment => fragment.Id).ToList();
        return await ExportInternalAsync(ids, options, progress, cancellationToken);
    }

    private async Task<ExportResult> ExportInternalAsync(
        IReadOnlyList<Guid> fragmentIds,
        MarkdownExportOptions options,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.TargetDirectory);
        var items = new List<ExportItem>();
        var filesWritten = 0;
        var attachmentsCopied = 0;
        var skipped = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = fragmentIds.Count;
        var current = 0;

        foreach (var fragmentId in fragmentIds)
        {
            current++;
            cancellationToken.ThrowIfCancellationRequested();
            var fragment = await _fragmentRepository.GetByIdAsync(fragmentId, cancellationToken);
            if (fragment is null)
            {
                skipped++;
                continue;
            }

            var streamId = await _fragmentRepository.GetStreamIdForFragmentAsync(fragmentId, cancellationToken);
            var stream = streamId.HasValue
                ? await _streamRepository.GetByIdAsync(streamId.Value, cancellationToken)
                : null;

            var fileName = _pathBuilder.BuildFileName(options, fragment);
            fileName = _pathBuilder.ResolveUniquePath(options.TargetDirectory, fileName, usedNames);
            var fullPath = Path.Combine(options.TargetDirectory, fileName);

            if (File.Exists(fullPath) && !options.OverwriteExisting)
            {
                skipped++;
                progress?.Report(new ExportProgress(current, total, fileName));
                continue;
            }

            var attachmentPaths = new List<string>();
            if (options.IncludeAttachments)
            {
                foreach (var payload in fragment.Payloads)
                {
                    if (fragment.Kind is FragmentKind.Image or FragmentKind.Files)
                    {
                        var path = await _attachmentCopier.CopyAsync(
                            payload.StorageKey,
                            options.TargetDirectory,
                            cancellationToken: cancellationToken);
                        if (path is not null)
                        {
                            attachmentPaths.Add(path);
                            attachmentsCopied++;
                        }
                    }
                }
            }

            await _markdownWriter.WriteAsync(fullPath, fragment, stream, attachmentPaths, options, cancellationToken);
            filesWritten++;
            items.Add(new ExportItem(fragment.Id, fileName, attachmentPaths.Count > 0 ? "attachments" : null));
            progress?.Report(new ExportProgress(current, total, fileName));
        }

        _logger.LogInformation("Export completed: {Files} files, {Attachments} attachments", filesWritten, attachmentsCopied);
        return new ExportResult(filesWritten, attachmentsCopied, skipped, items);
    }
}
