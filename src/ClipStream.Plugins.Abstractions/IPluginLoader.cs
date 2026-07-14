using ClipStream.Core.Models;

namespace ClipStream.Plugins.Abstractions;

public interface IPluginLoader
{
    IReadOnlyList<IClipboardFormatPlugin> FormatPlugins { get; }

    IReadOnlyList<IFragmentEnricherPlugin> EnricherPlugins { get; }

    IReadOnlyList<IFragmentActionPlugin> FragmentActionPlugins { get; }

    IReadOnlyList<IStreamActionPlugin> StreamActionPlugins { get; }

    Task ReloadAsync(CancellationToken cancellationToken = default);

    Task ActivateActionPluginsAsync(IPluginHost host, CancellationToken cancellationToken = default);

    Task DeactivateActionPluginsAsync(CancellationToken cancellationToken = default);
}

public interface IPluginPipeline
{
    Task<ClipboardFragment?> ProcessAsync(RawClipboardCapture capture, CancellationToken cancellationToken = default);
}
