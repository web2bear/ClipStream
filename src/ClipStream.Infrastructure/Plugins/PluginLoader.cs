using System.Reflection;
using System.Runtime.Loader;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace ClipStream.Infrastructure.Plugins;

public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}

public sealed class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly string _pluginsDirectory;

    private readonly List<IClipboardFormatPlugin> _builtInFormatPlugins = [];
    private readonly List<IFragmentEnricherPlugin> _builtInEnricherPlugins = [];
    private readonly List<IFragmentActionPlugin> _builtInFragmentActionPlugins = [];
    private readonly List<IStreamActionPlugin> _builtInStreamActionPlugins = [];
    private readonly List<IFragmentPreviewPlugin> _builtInPreviewPlugins = [];

    private readonly List<IClipboardFormatPlugin> _externalFormatPlugins = [];
    private readonly List<IFragmentEnricherPlugin> _externalEnricherPlugins = [];
    private readonly List<IFragmentActionPlugin> _externalFragmentActionPlugins = [];
    private readonly List<IStreamActionPlugin> _externalStreamActionPlugins = [];
    private readonly List<IFragmentPreviewPlugin> _externalPreviewPlugins = [];

    private readonly List<PluginLoadContext> _contexts = [];

    private List<IClipboardFormatPlugin> _formatPlugins = [];
    private List<IFragmentEnricherPlugin> _enricherPlugins = [];
    private List<IFragmentActionPlugin> _fragmentActionPlugins = [];
    private List<IStreamActionPlugin> _streamActionPlugins = [];
    private List<IFragmentPreviewPlugin> _previewPlugins = [];

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
        _pluginsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipStream",
            "plugins");
    }

    public IReadOnlyList<IClipboardFormatPlugin> FormatPlugins => _formatPlugins;

    public IReadOnlyList<IFragmentEnricherPlugin> EnricherPlugins => _enricherPlugins;

    public IReadOnlyList<IFragmentActionPlugin> FragmentActionPlugins => _fragmentActionPlugins;

    public IReadOnlyList<IStreamActionPlugin> StreamActionPlugins => _streamActionPlugins;

    public IReadOnlyList<IFragmentPreviewPlugin> PreviewPlugins => _previewPlugins;

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var previousContexts = _contexts.ToArray();
        _contexts.Clear();
        ClearExternalPlugins();

        if (Directory.Exists(_pluginsDirectory))
        {
            foreach (var dll in Directory.EnumerateFiles(_pluginsDirectory, "*.dll"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    LoadPluginsFromAssembly(dll);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load plugin assembly {Assembly}", dll);
                }
            }
        }

        RebuildPublicLists();
        UnloadContexts(previousContexts);

        return Task.CompletedTask;
    }

    public async Task ActivateActionPluginsAsync(IPluginHost host, CancellationToken cancellationToken = default)
    {
        await DeactivateActionPluginsAsync(cancellationToken);

        foreach (var plugin in _fragmentActionPlugins)
        {
            try
            {
                await plugin.ActivateAsync(host, cancellationToken);
                _logger.LogInformation("Activated fragment action plugin {Id}", plugin.Descriptor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to activate fragment action plugin {Id}", plugin.Descriptor.Id);
            }
        }

        foreach (var plugin in _streamActionPlugins)
        {
            try
            {
                await plugin.ActivateAsync(host, cancellationToken);
                _logger.LogInformation("Activated stream action plugin {Id}", plugin.Descriptor.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to activate stream action plugin {Id}", plugin.Descriptor.Id);
            }
        }
    }

    public async Task DeactivateActionPluginsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plugin in _fragmentActionPlugins)
        {
            try
            {
                await plugin.DeactivateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deactivate fragment action plugin {Id}", plugin.Descriptor.Id);
            }
        }

        foreach (var plugin in _streamActionPlugins)
        {
            try
            {
                await plugin.DeactivateAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deactivate stream action plugin {Id}", plugin.Descriptor.Id);
            }
        }
    }

    public void RegisterBuiltInPlugins(IEnumerable<IClipStreamPlugin> plugins)
    {
        foreach (var plugin in plugins)
        {
            switch (plugin)
            {
                case IClipboardFormatPlugin formatPlugin:
                    _builtInFormatPlugins.Add(formatPlugin);
                    break;
                case IFragmentEnricherPlugin enricherPlugin:
                    _builtInEnricherPlugins.Add(enricherPlugin);
                    break;
                case IFragmentActionPlugin fragmentActionPlugin:
                    _builtInFragmentActionPlugins.Add(fragmentActionPlugin);
                    break;
                case IStreamActionPlugin streamActionPlugin:
                    _builtInStreamActionPlugins.Add(streamActionPlugin);
                    break;
                case IFragmentPreviewPlugin previewPlugin:
                    _builtInPreviewPlugins.Add(previewPlugin);
                    break;
            }
        }

        RebuildPublicLists();
    }

    private void ClearExternalPlugins()
    {
        _externalFormatPlugins.Clear();
        _externalEnricherPlugins.Clear();
        _externalFragmentActionPlugins.Clear();
        _externalStreamActionPlugins.Clear();
        _externalPreviewPlugins.Clear();
    }

    private void RebuildPublicLists()
    {
        _formatPlugins = [.._builtInFormatPlugins, .._externalFormatPlugins];
        _enricherPlugins = [.._builtInEnricherPlugins, .._externalEnricherPlugins];
        _fragmentActionPlugins = [.._builtInFragmentActionPlugins, .._externalFragmentActionPlugins];
        _streamActionPlugins = [.._builtInStreamActionPlugins, .._externalStreamActionPlugins];
        _previewPlugins = [.._builtInPreviewPlugins, .._externalPreviewPlugins];
    }

    private void UnloadContexts(IEnumerable<PluginLoadContext> contexts)
    {
        foreach (var context in contexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unload plugin load context");
            }
        }
    }

    private void LoadPluginsFromAssembly(string assemblyPath)
    {
        var context = new PluginLoadContext(assemblyPath);
        _contexts.Add(context);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in assembly.GetTypes())
        {
            if (!typeof(IClipStreamPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not IClipStreamPlugin plugin)
            {
                continue;
            }

            switch (plugin)
            {
                case IClipboardFormatPlugin formatPlugin:
                    _externalFormatPlugins.Add(formatPlugin);
                    _logger.LogInformation("Loaded format plugin {Id}", formatPlugin.Descriptor.Id);
                    break;
                case IFragmentEnricherPlugin enricherPlugin:
                    _externalEnricherPlugins.Add(enricherPlugin);
                    _logger.LogInformation("Loaded enricher plugin {Id}", enricherPlugin.Descriptor.Id);
                    break;
                case IFragmentActionPlugin fragmentActionPlugin:
                    _externalFragmentActionPlugins.Add(fragmentActionPlugin);
                    _logger.LogInformation("Loaded fragment action plugin {Id}", fragmentActionPlugin.Descriptor.Id);
                    break;
                case IStreamActionPlugin streamActionPlugin:
                    _externalStreamActionPlugins.Add(streamActionPlugin);
                    _logger.LogInformation("Loaded stream action plugin {Id}", streamActionPlugin.Descriptor.Id);
                    break;
                case IFragmentPreviewPlugin previewPlugin:
                    _externalPreviewPlugins.Add(previewPlugin);
                    _logger.LogInformation("Loaded preview plugin {Id}", previewPlugin.Descriptor.Id);
                    break;
            }
        }
    }
}
