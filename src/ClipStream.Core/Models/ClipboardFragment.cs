namespace ClipStream.Core.Models;

public sealed record ClipboardFragment(
    Guid Id,
    DateTimeOffset CapturedAt,
    FragmentKind Kind,
    string? PreviewText,
    string? SourceProcessName,
    int? SourceProcessId,
    IReadOnlyList<FormatPayload> Payloads,
    IReadOnlyDictionary<string, string> Metadata,
    string? ContentHash = null);
