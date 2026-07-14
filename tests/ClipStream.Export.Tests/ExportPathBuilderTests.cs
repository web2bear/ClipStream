using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Export;

namespace ClipStream.Export.Tests;

public class ExportPathBuilderTests
{
    private readonly ExportPathBuilder _builder = new();

    [Fact]
    public void BuildFileName_TimestampAndSlug_UsesCapturedTimeAndPreview()
    {
        var fragment = CreateFragment(new DateTimeOffset(2026, 7, 13, 18, 30, 45, TimeSpan.Zero));
        var options = new MarkdownExportOptions
        {
            TargetDirectory = "C:\\export",
            FilenameStrategy = FilenameStrategy.TimestampAndSlug
        };

        var fileName = _builder.BuildFileName(options, fragment);

        Assert.StartsWith("183045-", fileName);
        Assert.EndsWith(".md", fileName);
    }

    [Fact]
    public void BuildFileName_Guid_UsesFragmentId()
    {
        var id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var fragment = CreateFragment(DateTimeOffset.UtcNow) with { Id = id };
        var options = new MarkdownExportOptions
        {
            TargetDirectory = "C:\\export",
            FilenameStrategy = FilenameStrategy.Guid
        };

        var fileName = _builder.BuildFileName(options, fragment);
        Assert.Equal($"{id:N}.md", fileName);
    }

    [Fact]
    public void BuildFileName_SlugFromTitle_UsesTitle()
    {
        var fragment = CreateFragment(DateTimeOffset.UtcNow);
        fragment.Title = "Мой важный фрагмент";
        var options = new MarkdownExportOptions
        {
            TargetDirectory = "C:\\export",
            FilenameStrategy = FilenameStrategy.SlugFromTitle
        };

        var fileName = _builder.BuildFileName(options, fragment);

        Assert.StartsWith("мой-важный-фрагмент", fileName);
        Assert.EndsWith(".md", fileName);
    }

    [Fact]
    public void BuildFileName_SlugFromTitle_DefaultStrategy_TextFragment()
    {
        var fragment = CreateFragment(new DateTimeOffset(2026, 7, 13, 18, 30, 45, TimeSpan.Zero));
        var options = new MarkdownExportOptions { TargetDirectory = "C:\\export" };

        var fileName = _builder.BuildFileName(options, fragment);

        Assert.StartsWith("hello", fileName);
        Assert.EndsWith(".md", fileName);
    }

    [Fact]
    public void BuildFileName_SlugFromTitle_DefaultStrategy_ImageFragment()
    {
        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 13, 18, 30, 45, TimeSpan.Zero),
            FragmentKind.Image,
            "[Image]",
            null,
            null,
            [],
            new Dictionary<string, string>(),
            null);
        var options = new MarkdownExportOptions { TargetDirectory = "C:\\export" };

        var fileName = _builder.BuildFileName(options, fragment);

        Assert.StartsWith("изображение-от-2026-07-13-18-30", fileName);
        Assert.EndsWith(".md", fileName);
    }

    [Fact]
    public void ResolveUniquePath_WithDuplicateTitles_AppendsIndex()
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string directory = "C:\\export";

        var first = _builder.ResolveUniquePath(directory, "фрагмент.md", usedNames);
        Assert.Equal("фрагмент.md", first);

        var second = _builder.ResolveUniquePath(directory, "фрагмент.md", usedNames);
        Assert.Equal("фрагмент-2.md", second);

        var third = _builder.ResolveUniquePath(directory, "фрагмент.md", usedNames);
        Assert.Equal("фрагмент-3.md", third);
    }

    private static ClipboardFragment CreateFragment(DateTimeOffset capturedAt) =>
        new(
            Guid.NewGuid(),
            capturedAt,
            FragmentKind.Text,
            "hello",
            "notepad.exe",
            123,
            [],
            new Dictionary<string, string>(),
            "sha256:abc");
}
