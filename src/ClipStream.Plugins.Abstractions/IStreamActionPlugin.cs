namespace ClipStream.Plugins.Abstractions;

public interface IStreamActionPlugin : IClipStreamLifecyclePlugin
{
    string MenuTitle { get; }

    string? MenuGroup { get; }

    int MenuOrder { get; }

    Task<bool> CanExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default);

    Task ExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default);
}
