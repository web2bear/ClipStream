using ClipStream.Plugins.Abstractions;

namespace ClipStream.Core.Tests;

public class ContentHashHelperTests
{
    [Fact]
    public void ComputeCaptureHash_SameFormats_ProducesStableHash()
    {
        var formats = new List<RawFormatData>
        {
            new("UnicodeText", "hello"u8.ToArray()),
            new("HTML Format", "<b>h</b>"u8.ToArray())
        };

        var hash1 = ContentHashHelper.ComputeCaptureHash(formats);
        var hash2 = ContentHashHelper.ComputeCaptureHash(formats);

        Assert.Equal(hash1, hash2);
        Assert.StartsWith("sha256:", hash1);
    }
}
