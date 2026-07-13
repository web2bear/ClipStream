namespace ClipStream.App;

public static class StreamIcons
{
    public const string DefaultKey = "folder";

    public sealed record IconOption(string Key, string Glyph);

    public static IReadOnlyList<IconOption> All { get; } =
    [
        new("inbox", "\uE715"),
        new("folder", "\uE8B7"),
        new("stream", "\uE8F1"),
        new("star", "\uE734"),
        new("bookmark", "\uE8A4"),
        new("tag", "\uE8EC"),
        new("document", "\uE8A5"),
        new("image", "\uEB9F"),
        new("link", "\uE71B"),
        new("code", "\uE943"),
        new("pin", "\uE718"),
        new("heart", "\uEB51"),
    ];

    private static readonly Dictionary<string, string> GlyphByKey = All.ToDictionary(
        icon => icon.Key,
        icon => icon.Glyph,
        StringComparer.OrdinalIgnoreCase);

    public static string GetGlyph(string? key) =>
        key is not null && GlyphByKey.TryGetValue(key, out var glyph) ? glyph : GlyphByKey[DefaultKey];
}
