namespace ClipStream.Core.Models;

public sealed record ClipStreamEntity(
    Guid Id,
    string Name,
    string? Icon,
    int SortOrder,
    bool IsPinned);
