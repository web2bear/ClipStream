using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Storage;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipStream.Infrastructure.Plugins;

public sealed class PluginPipeline : IPluginPipeline
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IBlobStore _blobStore;
    private readonly IFragmentRepository _fragmentRepository;
    private readonly ILogger<PluginPipeline> _logger;

    public PluginPipeline(
        IPluginLoader pluginLoader,
        IBlobStore blobStore,
        IFragmentRepository fragmentRepository,
        ILogger<PluginPipeline> logger)
    {
        _pluginLoader = pluginLoader;
        _blobStore = blobStore;
        _fragmentRepository = fragmentRepository;
        _logger = logger;
    }

    public async Task<ClipboardFragment?> ProcessAsync(RawClipboardCapture capture, CancellationToken cancellationToken = default)
    {
        var recent = await _fragmentRepository.GetRecentAsync(20, cancellationToken);
        var context = new PluginContext(_blobStore, recent);

        ClipboardFragment? fragment = null;

        foreach (var plugin in _pluginLoader.FormatPlugins.OrderBy(p => p.Descriptor.Priority))
        {
            if (!plugin.CanHandle(capture))
            {
                continue;
            }

            try
            {
                var result = await plugin.ProcessAsync(capture, context, cancellationToken);
                fragment = result switch
                {
                    FragmentProduced produced => produced.Fragment,
                    Enriched enriched => enriched.Fragment with
                    {
                        Payloads = enriched.Fragment.Payloads.Concat(enriched.ExtraPayloads).ToList()
                    },
                    Skipped skipped => null,
                    _ => fragment
                };

                if (fragment is not null)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin {PluginId} failed", plugin.Descriptor.Id);
            }
        }

        if (fragment is null)
        {
            return null;
        }

        foreach (var enricher in _pluginLoader.EnricherPlugins.OrderBy(p => p.Descriptor.Priority))
        {
            if (!enricher.CanEnrich(fragment))
            {
                continue;
            }

            try
            {
                fragment = await enricher.EnrichAsync(fragment, context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Enricher {PluginId} failed", enricher.Descriptor.Id);
            }
        }

        if (!string.IsNullOrEmpty(fragment.ContentHash))
        {
            var exists = await _fragmentRepository.ExistsByContentHashAsync(fragment.ContentHash, cancellationToken);
            if (exists)
            {
                return null;
            }
        }

        return fragment;
    }

    public static string ComputeContentHash(IReadOnlyList<RawFormatData> formats)
        => ContentHashHelper.ComputeCaptureHash(formats);
}
