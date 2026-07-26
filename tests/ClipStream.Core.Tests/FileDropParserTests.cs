using System.Text;
using ClipStream.Core;

namespace ClipStream.Core.Tests;

public class FileDropParserTests
{
    [Fact]
    public void Parse_UnicodeDropFiles_ReturnsPaths()
    {
        var path1 = @"C:\Videos\clip.mp4";
        var path2 = @"C:\Videos\Folder";
        var data = BuildUnicodeDropFiles(path1, path2);

        var paths = FileDropParser.Parse(data);

        Assert.Equal([path1, path2], paths);
    }

    [Fact]
    public void Parse_DoesNotStopOnEmbeddedNullHighBytes()
    {
        // Regression: searching for a single 0x00 byte breaks UTF-16LE paths like "C:\..."
        // where every ASCII wchar is [char, 0x00].
        var path = @"D:\media\video.mkv";
        var data = BuildUnicodeDropFiles(path);

        var paths = FileDropParser.Parse(data);

        Assert.Equal([path], paths);
        Assert.DoesNotContain('\0', paths[0]);
    }

    [Fact]
    public void Parse_AnsiDropFiles_ReturnsPaths()
    {
        var path = @"C:\temp\file.txt";
        var data = BuildAnsiDropFiles(path);

        var paths = FileDropParser.Parse(data);

        Assert.Equal([path], paths);
    }

    [Fact]
    public void Parse_BareUnicodeFileName_ReturnsSinglePath()
    {
        var path = @"C:\only\one.bin";
        var bytes = Encoding.Unicode.GetBytes(path + "\0");

        var paths = FileDropParser.Parse(bytes);

        Assert.Equal([path], paths);
    }

    private static byte[] BuildUnicodeDropFiles(params string[] paths)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(20); // pFiles
        writer.Write(0);  // pt.x
        writer.Write(0);  // pt.y
        writer.Write(0);  // fNC
        writer.Write(1);  // fWide = TRUE

        foreach (var path in paths)
        {
            writer.Write(Encoding.Unicode.GetBytes(path));
            writer.Write((ushort)0);
        }

        writer.Write((ushort)0); // final double-null
        return stream.ToArray();
    }

    private static byte[] BuildAnsiDropFiles(params string[] paths)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(20); // pFiles
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0); // fWide = FALSE

        foreach (var path in paths)
        {
            writer.Write(Encoding.Default.GetBytes(path));
            writer.Write((byte)0);
        }

        writer.Write((byte)0);
        return stream.ToArray();
    }
}
