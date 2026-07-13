using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Export;

namespace ClipStream.Export.Tests;

public class MarkdownFragmentWriterTests
{
    [Fact]
    public async Task WriteAsync_TextFragment_ContainsFrontmatterAndBody()
    {
        var writer = new MarkdownFragmentWriter();
        var tempDir = Path.Combine(Path.GetTempPath(), "clipstream-test-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "test.md");

        try
        {
            var fragment = new ClipboardFragment(
                Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                new DateTimeOffset(2026, 7, 13, 18, 30, 45, TimeSpan.FromHours(3)),
                FragmentKind.Text,
                "Hello world",
                "Code.exe",
                42,
                [new FormatPayload("UnicodeText", "ab/hash", 11, "hash")],
                new Dictionary<string, string>(),
                "sha256:abc123");

            var stream = new ClipStreamEntity(Guid.NewGuid(), "work", "briefcase", 1, false);
            var options = new ObsidianExportOptions { TargetDirectory = tempDir };

            await writer.WriteAsync(filePath, fragment, stream, [], [], options);

            var content = await File.ReadAllTextAsync(filePath);
            Assert.StartsWith("---", content);
            Assert.Contains("id: a1b2c3d4-e5f6-7890-abcd-ef1234567890", content);
            Assert.Contains("stream: work", content);
            Assert.Contains("Hello world", content);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
