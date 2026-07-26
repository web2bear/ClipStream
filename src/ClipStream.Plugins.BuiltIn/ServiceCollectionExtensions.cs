using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ClipStream.Plugins.BuiltIn;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuiltInPlugins(this IServiceCollection services)
    {
        services.AddSingleton<IClipStreamPlugin, TextFormatPlugin>();
        services.AddSingleton<IClipStreamPlugin, HtmlFormatPlugin>();
        services.AddSingleton<IClipStreamPlugin, ImageFormatPlugin>();
        services.AddSingleton<IClipStreamPlugin, FilesFormatPlugin>();
        services.AddSingleton<IClipStreamPlugin, GenericBinaryPlugin>();
        services.AddSingleton<IClipStreamPlugin, TextPreviewPlugin>();
        services.AddSingleton<IClipStreamPlugin, ImagePreviewPlugin>();
        services.AddSingleton<IClipStreamPlugin, FilesPreviewPlugin>();
        services.AddSingleton<IClipStreamPlugin, PasteFragmentActionPlugin>();
        services.AddSingleton<IClipStreamPlugin, ExportFragmentMarkdownActionPlugin>();
        services.AddSingleton<IClipStreamPlugin, ExportStreamMarkdownActionPlugin>();
        return services;
    }

    public static void RegisterBuiltInPluginsWithLoader(IServiceProvider services)
    {
        var loader = services.GetRequiredService<Infrastructure.Plugins.PluginLoader>();
        var plugins = services.GetServices<IClipStreamPlugin>();
        loader.RegisterBuiltInPlugins(plugins);
    }
}
