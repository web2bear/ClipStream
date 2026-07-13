namespace ClipStream.Plugins.Abstractions;

public interface IClipboardFormatPlugin : IClipStreamPlugin
{
    bool CanHandle(RawClipboardCapture capture);

    Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken);
}
