using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Core.Storage;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Infrastructure.Plugins;
using ClipStream.Infrastructure.Routing;
using ClipStream.Infrastructure.Storage;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ClipStream.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClipStreamInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ClipStreamDatabase>();
        services.AddSingleton<IBlobStore, FileBlobStore>();
        services.AddSingleton<IFragmentRepository, SqliteFragmentRepository>();
        services.AddSingleton<IStreamRepository, SqliteStreamRepository>();
        services.AddSingleton<IRoutingRuleRepository, SqliteRoutingRuleRepository>();
        services.AddSingleton<IRoutingEngine, RoutingEngine>();
        services.AddSingleton<PluginLoader>();
        services.AddSingleton<IPluginLoader>(sp => sp.GetRequiredService<PluginLoader>());
        services.AddSingleton<IPluginPipeline, PluginPipeline>();
        return services;
    }
}
