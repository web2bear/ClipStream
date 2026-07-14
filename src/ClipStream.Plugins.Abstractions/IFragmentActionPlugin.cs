namespace ClipStream.Plugins.Abstractions;

public interface IFragmentActionPlugin : IClipStreamLifecyclePlugin
{
    string MenuTitle { get; }

    string? MenuGroup { get; }

    int MenuOrder { get; }

    Task<bool> CanExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default);

    Task ExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default);
}
