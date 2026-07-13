using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Export;

namespace ClipStream.Export.Tests;

public class ExportPathBuilderTests
{
    private readonly ExportPathBuilder _builder = new();

    [Fact]
    public void BuildFragmentPath_StreamsAsFolders_IncludesStreamAndDate()
    {
        var fragment = CreateFragment(new DateTimeOffset(2026, 7, 13, 18, 30, 45, TimeSpan.Zero));
        var stream = new ClipStreamEntity(Guid.NewGuid(), "Work Items", null, 0, false);
        var options = new ObsidianExportOptions { TargetDirectory = "C:\\vault" };

        var path = _builder.BuildFragmentPath(options, fragment, stream, "183045-test.md");

        Assert.Equal("streams/work-items/2026/07/13/183045-test.md", path.Replace('\\', '/'));
    }

    [Fact]
    public void BuildFileName_Guid_UsesFragmentId()
    {
        var id = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var fragment = CreateFragment(DateTimeOffset.UtcNow) with { Id = id };
        var options = new ObsidianExportOptions
        {
            TargetDirectory = "C:\\vault",
            FilenameStrategy = FilenameStrategy.Guid
        };

        var fileName = _builder.BuildFileName(options, fragment);
        Assert.Equal($"{id:N}.md", fileName);
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
