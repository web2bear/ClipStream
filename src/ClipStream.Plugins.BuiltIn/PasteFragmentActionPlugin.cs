using ClipStream.Core.Paste;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Plugins.BuiltIn;

public sealed class PasteFragmentActionPlugin : BuiltInPluginBase, IFragmentActionPlugin
{
    private IFragmentPasteService? _pasteService;

    public override PluginDescriptor Descriptor { get; } = new("builtin.action.paste", "Paste to active window", "1.0.0", 10);

    public string MenuTitle => "Paste to active window";

    public string? MenuGroup => "Clipboard";

    public int MenuOrder => 0;

    public Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default)
    {
        _pasteService = host.Services.GetService(typeof(IFragmentPasteService)) as IFragmentPasteService;
        return Task.CompletedTask;
    }

    public Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        _pasteService = null;
        return Task.CompletedTask;
    }

    public Task<bool> CanExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_pasteService is not null);

    public async Task ExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default)
    {
        if (_pasteService is null)
        {
            context.ReportStatus("Paste is not available");
            return;
        }

        try
        {
            var fullFragment = await context.Fragments.GetByIdAsync(context.Fragment.Id, cancellationToken)
                ?? context.Fragment;
            await _pasteService.PasteToActiveWindowAsync(fullFragment, cancellationToken);
            context.ReportStatus("Pasted to active window");
        }
        catch (Exception ex)
        {
            context.ReportStatus($"Paste failed: {ex.Message}");
        }
    }
}
