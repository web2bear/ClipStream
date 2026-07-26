using System.Text;

namespace ClipStream.Core;

/// <summary>
/// Parses CF_HDROP / DROPFILES clipboard payloads into file system paths.
/// </summary>
public static class FileDropParser
{
    public static IReadOnlyList<string> Parse(byte[] data)
    {
        if (data.Length < 4)
        {
            return [];
        }

        if (data.Length >= 20)
        {
            var pFiles = BitConverter.ToInt32(data, 0);
            if (pFiles is >= 20 and < int.MaxValue && pFiles < data.Length)
            {
                var fWide = BitConverter.ToInt32(data, 16) != 0;
                var paths = fWide ? ParseWideList(data, pFiles) : ParseAnsiList(data, pFiles);
                if (paths.Count > 0)
                {
                    return paths;
                }
            }
        }

        // FileNameW / bare Unicode path (no DROPFILES header).
        return ParseWideList(data, 0);
    }

    private static List<string> ParseWideList(byte[] data, int offset)
    {
        var paths = new List<string>();
        while (offset + 1 < data.Length)
        {
            if (data[offset] == 0 && data[offset + 1] == 0)
            {
                break;
            }

            var end = offset;
            while (end + 1 < data.Length && !(data[end] == 0 && data[end + 1] == 0))
            {
                end += 2;
            }

            var byteLength = end - offset;
            if (byteLength > 0)
            {
                paths.Add(Encoding.Unicode.GetString(data, offset, byteLength));
            }

            // Skip the terminating wchar null; if we ran out of bytes, stop.
            if (end + 1 >= data.Length)
            {
                break;
            }

            offset = end + 2;
        }

        return paths;
    }

    private static List<string> ParseAnsiList(byte[] data, int offset)
    {
        var paths = new List<string>();
        while (offset < data.Length)
        {
            if (data[offset] == 0)
            {
                break;
            }

            var end = Array.IndexOf(data, (byte)0, offset);
            if (end < 0)
            {
                paths.Add(Encoding.Default.GetString(data, offset, data.Length - offset));
                break;
            }

            if (end > offset)
            {
                paths.Add(Encoding.Default.GetString(data, offset, end - offset));
            }

            offset = end + 1;
        }

        return paths;
    }
}
