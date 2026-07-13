using ClipStream.Core.Models;

namespace ClipStream.Plugins.Abstractions;

public interface IPluginLoader
{
    IReadOnlyList<IClipboardFormatPlugin> FormatPlugins { get; }

    IReadOnlyList<IFragmentEnricherPlugin> EnricherPlugins { get; }

    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public interface IPluginPipeline
{
    Task<ClipboardFragment?> ProcessAsync(RawClipboardCapture capture, CancellationToken cancellationToken = default);
}
