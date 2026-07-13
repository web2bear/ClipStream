using ClipStream.Core.Export;
using Microsoft.Extensions.DependencyInjection;

namespace ClipStream.Export;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClipStreamExport(this IServiceCollection services)
    {
        services.AddSingleton<ExportPathBuilder>();
        services.AddSingleton<MarkdownFragmentWriter>();
        services.AddSingleton<AttachmentCopier>();
        services.AddSingleton<IObsidianVaultExporter, ObsidianVaultExporter>();
        return services;
    }
}
