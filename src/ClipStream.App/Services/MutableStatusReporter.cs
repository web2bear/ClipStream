namespace ClipStream.App.Services;

public sealed class MutableStatusReporter : IStatusReporter
{
    private Action<string>? _handler;

    public void SetHandler(Action<string> handler) => _handler = handler;

    public void ReportStatus(string message) => _handler?.Invoke(message);
}
