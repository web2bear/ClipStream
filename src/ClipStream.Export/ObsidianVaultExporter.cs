using System.Text.Json;
using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace ClipStream.Export;

public sealed class ObsidianVaultExporter : IObsidianVaultExporter
{
    private readonly IFragmentRepository _fragmentRepository;
    private readonly IStreamRepository _streamRepository;
    private readonly ExportPathBuilder _pathBuilder;
    private readonly MarkdownFragmentWriter _markdownWriter;
    private readonly AttachmentCopier _attachmentCopier;
    private readonly ILogger<ObsidianVaultExporter> _logger;

    public ObsidianVaultExporter(
        IFragmentRepository fragmentRepository,
        IStreamRepository streamRepository,
        ExportPathBuilder pathBuilder,
        MarkdownFragmentWriter markdownWriter,
        AttachmentCopier attachmentCopier,
        ILogger<ObsidianVaultExporter> logger)
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
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => ExportInternalAsync([fragmentId], options, writeStreamIndexes: false, progress, cancellationToken);

    public async Task<ExportResult> ExportStreamAsync(
        Guid streamId,
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stream = await _streamRepository.GetByIdAsync(streamId, cancellationToken);
        if (stream is null)
        {
            return new ExportResult(0, 0, 0, [], null);
        }

        var fragments = await _fragmentRepository.GetByStreamAsync(streamId, 0, int.MaxValue, cancellationToken);
        var ids = fragments.Select(f => f.Id).ToList();
        return await ExportInternalAsync(ids, options, writeStreamIndexes: true, progress, cancellationToken, [stream]);
    }

    public async Task<ExportResult> ExportAllAsync(
        ObsidianExportOptions options,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var streams = await _streamRepository.GetAllAsync(cancellationToken);
        var fragments = await _fragmentRepository.GetAllAsync(cancellationToken: cancellationToken);
        var ids = fragments.Select(f => f.Id).ToList();
        return await ExportInternalAsync(ids, options, writeStreamIndexes: true, progress, cancellationToken, streams);
    }

    private async Task<ExportResult> ExportInternalAsync(
        IReadOnlyList<Guid> fragmentIds,
        ObsidianExportOptions options,
        bool writeStreamIndexes,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken,
        IReadOnlyList<ClipStreamEntity>? streamsForIndex = null)
    {
        Directory.CreateDirectory(options.TargetDirectory);
        var items = new List<ExportItem>();
        var filesWritten = 0;
        var attachmentsCopied = 0;
        var skipped = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (writeStreamIndexes)
        {
            var streams = streamsForIndex ?? await _streamRepository.GetAllAsync(cancellationToken);
            foreach (var stream in streams)
            {
                var indexPath = Path.Combine(options.TargetDirectory, _pathBuilder.BuildStreamIndexPath(stream));
                await _markdownWriter.WriteStreamIndexAsync(indexPath, stream, cancellationToken);
                filesWritten++;
            }
        }

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
            var relativePath = _pathBuilder.BuildFragmentPath(options, fragment, stream, fileName);
            var directory = Path.GetDirectoryName(Path.Combine(options.TargetDirectory, relativePath))!;
            fileName = _pathBuilder.ResolveUniquePath(directory, Path.GetFileName(relativePath), usedNames);
            relativePath = Path.Combine(Path.GetDirectoryName(relativePath) ?? string.Empty, fileName).Replace('\\', '/');
            var fullPath = Path.Combine(options.TargetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(fullPath) && !options.OverwriteExisting)
            {
                skipped++;
                progress?.Report(new ExportProgress(current, total, relativePath));
                continue;
            }

            var attachmentPaths = new List<string>();
            var rawPaths = new List<string>();
            if (options.IncludeAttachments)
            {
                foreach (var payload in fragment.Payloads)
                {
                    if (fragment.Kind is FragmentKind.Image or FragmentKind.Files)
                    {
                        var path = await _attachmentCopier.CopyAsync(payload.StorageKey, options.TargetDirectory, cancellationToken: cancellationToken);
                        if (path is not null)
                        {
                            attachmentPaths.Add(path);
                            attachmentsCopied++;
                        }
                    }
                    else if (options.IncludeRawFormats)
                    {
                        var path = await _attachmentCopier.CopyRawAsync(payload.StorageKey, payload.FormatName, options.TargetDirectory, cancellationToken);
                        if (path is not null)
                        {
                            rawPaths.Add(path);
                            attachmentsCopied++;
                        }
                    }
                }
            }

            await _markdownWriter.WriteAsync(fullPath, fragment, stream, attachmentPaths, rawPaths, options, cancellationToken);
            filesWritten++;
            items.Add(new ExportItem(fragment.Id, relativePath, attachmentPaths.Count > 0 ? "attachments" : null));
            progress?.Report(new ExportProgress(current, total, relativePath));
        }

        var manifestPath = await WriteManifestAsync(options.TargetDirectory, items, cancellationToken);
        _logger.LogInformation("Export completed: {Files} files, {Attachments} attachments", filesWritten, attachmentsCopied);
        return new ExportResult(filesWritten, attachmentsCopied, skipped, items, manifestPath);
    }

    private static async Task<string> WriteManifestAsync(
        string targetDirectory,
        IReadOnlyList<ExportItem> items,
        CancellationToken cancellationToken)
    {
        var manifestDir = Path.Combine(targetDirectory, ".clipstream");
        Directory.CreateDirectory(manifestDir);
        var manifestPath = Path.Combine(manifestDir, "export-manifest.json");
        var manifest = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            clipstreamVersion = "1.0.0",
            items = items.Select(i => new
            {
                fragmentId = i.FragmentId,
                relativePath = i.RelativePath
            })
        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return manifestPath;
    }
}
