using System.IO;
using ClipStream.Clipboard;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Core.Storage;
using ClipStream.Infrastructure;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Infrastructure.Plugins;
using ClipStream.Infrastructure.Storage;
using ClipStream.Plugins.Abstractions;
using ClipStream.Plugins.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipStream.Clipboard.Tests;

internal static class TestServiceFactory
{
    public static ServiceProvider Create(string tempRoot, LogLevel logLevel = LogLevel.Debug)
    {
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "clipstream.db");
        var blobPath = Path.Combine(tempRoot, "blobs");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(logLevel));
        services.AddSingleton(new ClipStreamDatabase(dbPath));
        services.AddSingleton<IBlobStore>(_ => new FileBlobStore(blobPath));
        services.AddSingleton<IFragmentRepository, SqliteFragmentRepository>();
        services.AddSingleton<IStreamRepository, SqliteStreamRepository>();
        services.AddSingleton<IRoutingRuleRepository, SqliteRoutingRuleRepository>();
        services.AddSingleton<IRoutingEngine, Infrastructure.Routing.RoutingEngine>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<IPluginLoader>(sp => sp.GetRequiredService<PluginLoader>());
        services.AddSingleton<IPluginPipeline, PluginPipeline>();
        services.AddClipStreamClipboard();
        services.AddBuiltInPlugins();

        var provider = services.BuildServiceProvider();
        var loader = provider.GetRequiredService<PluginLoader>();
        loader.RegisterBuiltInPlugins(provider.GetServices<IClipStreamPlugin>());
        return provider;
    }
}
