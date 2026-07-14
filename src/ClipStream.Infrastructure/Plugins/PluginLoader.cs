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
    private readonly List<IClipboardFormatPlugin> _formatPlugins = [];
    private readonly List<IFragmentEnricherPlugin> _enricherPlugins = [];
    private readonly List<IFragmentActionPlugin> _fragmentActionPlugins = [];
    private readonly List<IStreamActionPlugin> _streamActionPlugins = [];
    private readonly List<PluginLoadContext> _contexts = [];

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

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _formatPlugins.Clear();
        _enricherPlugins.Clear();
        _fragmentActionPlugins.Clear();
        _streamActionPlugins.Clear();
        _contexts.Clear();

        if (Directory.Exists(_pluginsDirectory))
        {
            foreach (var dll in Directory.EnumerateFiles(_pluginsDirectory, "*.dll"))
            {
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
                    _formatPlugins.Add(formatPlugin);
                    break;
                case IFragmentEnricherPlugin enricherPlugin:
                    _enricherPlugins.Add(enricherPlugin);
                    break;
                case IFragmentActionPlugin fragmentActionPlugin:
                    _fragmentActionPlugins.Add(fragmentActionPlugin);
                    break;
                case IStreamActionPlugin streamActionPlugin:
                    _streamActionPlugins.Add(streamActionPlugin);
                    break;
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
                    _formatPlugins.Add(formatPlugin);
                    _logger.LogInformation("Loaded format plugin {Id}", formatPlugin.Descriptor.Id);
                    break;
                case IFragmentEnricherPlugin enricherPlugin:
                    _enricherPlugins.Add(enricherPlugin);
                    _logger.LogInformation("Loaded enricher plugin {Id}", enricherPlugin.Descriptor.Id);
                    break;
                case IFragmentActionPlugin fragmentActionPlugin:
                    _fragmentActionPlugins.Add(fragmentActionPlugin);
                    _logger.LogInformation("Loaded fragment action plugin {Id}", fragmentActionPlugin.Descriptor.Id);
                    break;
                case IStreamActionPlugin streamActionPlugin:
                    _streamActionPlugins.Add(streamActionPlugin);
                    _logger.LogInformation("Loaded stream action plugin {Id}", streamActionPlugin.Descriptor.Id);
                    break;
            }
        }
    }
}
