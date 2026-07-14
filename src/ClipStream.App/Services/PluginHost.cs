using ClipStream.Plugins.Abstractions;

namespace ClipStream.App.Services;

public sealed class PluginHost : IPluginHost
{
    public PluginHost(IServiceProvider services, IPluginDialogs dialogs, IStatusReporter statusReporter)
    {
        Services = services;
        Dialogs = dialogs;
        _statusReporter = statusReporter;
    }

    private readonly IStatusReporter _statusReporter;

    public IServiceProvider Services { get; }

    public IPluginDialogs Dialogs { get; }

    public void ReportStatus(string message) => _statusReporter.ReportStatus(message);
}
