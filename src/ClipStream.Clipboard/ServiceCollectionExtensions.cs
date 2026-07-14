using ClipStream.Clipboard.Capture;
using ClipStream.Clipboard.Guard;
using ClipStream.Clipboard.Listener;
using ClipStream.Clipboard.Paste;
using ClipStream.Core.Paste;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClipStream.Clipboard;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClipStreamClipboard(this IServiceCollection services)
    {
        services.AddSingleton<IClipboardOwnershipGuard, ClipboardOwnershipGuard>();
        services.AddSingleton<IClipboardListener, Win32ClipboardListener>();
        services.AddSingleton<IClipboardCaptureService, ClipboardCaptureService>();
        services.AddSingleton<IClipboardPayloadBuilder, ClipboardPayloadBuilder>();
        services.AddSingleton<ForegroundWindowTracker>();
        services.AddSingleton<IForegroundWindowTracker>(sp => sp.GetRequiredService<ForegroundWindowTracker>());
        services.AddHostedService(sp => sp.GetRequiredService<ForegroundWindowTracker>());
        services.AddSingleton<IClipboardWriter, ClipboardWriter>();
        services.AddSingleton<IFragmentPasteService, FragmentPasteService>();
        return services;
    }
}
