namespace ClipStream.Plugins.Abstractions;

public interface IPluginHost
{
    IServiceProvider Services { get; }

    IPluginDialogs Dialogs { get; }

    void ReportStatus(string message);
}

public interface IPluginDialogs
{
    Task<string?> PickFolderAsync(string description, CancellationToken cancellationToken = default);
}
