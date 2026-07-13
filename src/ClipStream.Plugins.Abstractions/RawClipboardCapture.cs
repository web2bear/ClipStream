namespace ClipStream.Plugins.Abstractions;

public sealed record RawFormatData(string FormatName, byte[] Data);

public sealed record RawClipboardCapture(
    DateTimeOffset CapturedAt,
    uint ClipboardSequence,
    string? SourceProcessName,
    int? SourceProcessId,
    IReadOnlyList<RawFormatData> Formats);
