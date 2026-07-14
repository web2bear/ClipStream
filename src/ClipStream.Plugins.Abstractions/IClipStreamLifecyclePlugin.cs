namespace ClipStream.Plugins.Abstractions;

public interface IClipStreamLifecyclePlugin : IClipStreamPlugin
{
    Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default);

    Task DeactivateAsync(CancellationToken cancellationToken = default);
}
