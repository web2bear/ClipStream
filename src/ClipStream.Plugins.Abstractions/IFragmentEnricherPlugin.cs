using ClipStream.Core.Models;

namespace ClipStream.Plugins.Abstractions;

public interface IFragmentEnricherPlugin : IClipStreamPlugin
{
    bool CanEnrich(ClipboardFragment fragment);

    Task<ClipboardFragment> EnrichAsync(
        ClipboardFragment fragment,
        PluginContext context,
        CancellationToken cancellationToken);
}
