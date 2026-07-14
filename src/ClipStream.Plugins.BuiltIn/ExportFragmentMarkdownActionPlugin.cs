using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class ExportFragmentMarkdownActionPlugin : BuiltInPluginBase, IFragmentActionPlugin
{
    private readonly IMarkdownExporter _exporter;

    public ExportFragmentMarkdownActionPlugin(IMarkdownExporter exporter) => _exporter = exporter;

    public override PluginDescriptor Descriptor { get; } = new(
        "builtin.action.export-fragment-markdown",
        "Export fragment to folder",
        "1.0.0",
        20);

    public string MenuTitle => "Export to folder...";

    public string? MenuGroup => "Export";

    public int MenuOrder => 0;

    public Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CanExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(context.Fragment.Kind.IsTextKind());

    public async Task ExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Fragment.Kind.IsTextKind())
        {
            context.ReportStatus("Only text fragments can be exported");
            return;
        }

        try
        {
            context.ReportStatus("Выберите папку для экспорта...");
            var path = await context.Dialogs.PickFolderAsync("Выберите папку для сохранения", cancellationToken);
            if (path is null)
            {
                return;
            }

            var options = new MarkdownExportOptions
            {
                TargetDirectory = path,
                IncludeAttachments = false
            };

            var result = await _exporter.ExportFragmentAsync(context.Fragment.Id, options, progress: null, cancellationToken);
            context.ReportStatus($"Exported {result.FilesWritten} file(s) to {path}");
        }
        catch (Exception ex)
        {
            context.ReportStatus($"Export failed: {ex.Message}");
        }
    }
}
