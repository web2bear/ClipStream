namespace ClipStream.Core.Models;

public sealed record FormatPayload(
    string FormatName,
    string StorageKey,
    long SizeBytes,
    string? ContentHash);
