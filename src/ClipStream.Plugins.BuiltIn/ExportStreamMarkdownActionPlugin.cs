using ClipStream.Core.Export;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class ExportStreamMarkdownActionPlugin : BuiltInPluginBase, IStreamActionPlugin
{
    private readonly IMarkdownExporter _exporter;

    public ExportStreamMarkdownActionPlugin(IMarkdownExporter exporter) => _exporter = exporter;

    public override PluginDescriptor Descriptor { get; } = new(
        "builtin.action.export-stream-markdown",
        "Export stream to folder",
        "1.0.0",
        20);

    public string MenuTitle => "Export to folder...";

    public string? MenuGroup => "Export";

    public int MenuOrder => 0;

    public Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CanExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public async Task ExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            context.ReportStatus("Выберите папку для экспорта...");
            var path = await context.Dialogs.PickFolderAsync("Выберите папку для сохранения", cancellationToken);
            if (path is null)
            {
                return;
            }

            var options = new MarkdownExportOptions { TargetDirectory = path };
            var result = await _exporter.ExportStreamAsync(context.Stream.Id, options, progress: null, cancellationToken);
            context.ReportStatus(
                $"Exported stream \"{context.Stream.Name}\": {result.FilesWritten} file(s), {result.AttachmentsCopied} attachment(s)");
        }
        catch (Exception ex)
        {
            context.ReportStatus($"Export failed: {ex.Message}");
        }
    }
}
