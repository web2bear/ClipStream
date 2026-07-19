using ClipStream.Clipboard.Capture;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.Clipboard.Tests;

public class ClipboardPrivacyFilterTests
{
    [Fact]
    public void ShouldIgnore_ClipboardViewerIgnoreFormat()
    {
        var capture = CreateCapture(("Clipboard Viewer Ignore", [1]));
        Assert.True(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    [Fact]
    public void ShouldIgnore_ExcludeClipboardContentFromMonitorProcessing()
    {
        var capture = CreateCapture(("ExcludeClipboardContentFromMonitorProcessing", [(byte)'1']));
        Assert.True(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    [Fact]
    public void ShouldIgnore_CanIncludeInClipboardHistoryZero()
    {
        var capture = CreateCapture(
            ("CF_UNICODETEXT", [0x41, 0, 0]),
            ("CanIncludeInClipboardHistory", [0, 0, 0, 0]));
        Assert.True(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    [Fact]
    public void ShouldIgnore_CanUploadToCloudClipboardZero()
    {
        var capture = CreateCapture(("CanUploadToCloudClipboard", [0, 0, 0, 0]));
        Assert.True(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    [Fact]
    public void ShouldIgnore_FalseForNormalText()
    {
        var capture = CreateCapture(("CF_UNICODETEXT", [0x41, 0, 0]));
        Assert.False(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    [Fact]
    public void ShouldIgnore_FalseWhenCanIncludeIsNonZero()
    {
        var capture = CreateCapture(("CanIncludeInClipboardHistory", [1, 0, 0, 0]));
        Assert.False(ClipboardPrivacyFilter.ShouldIgnore(capture));
    }

    private static RawClipboardCapture CreateCapture(params (string Name, byte[] Data)[] formats) =>
        new(
            DateTimeOffset.UtcNow,
            1,
            "test.exe",
            1,
            formats.Select(f => new RawFormatData(f.Name, f.Data)).ToList());
}
