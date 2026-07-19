using ClipStream.Plugins.Abstractions;

namespace ClipStream.Clipboard.Capture;

public static class ClipboardPrivacyFilter
{
    private static readonly byte[] ZeroDWord = [0, 0, 0, 0];

    public static bool ShouldIgnore(RawClipboardCapture capture)
    {
        foreach (var format in capture.Formats)
        {
            if (IsIgnoreByFormatName(format.FormatName))
            {
                return true;
            }

            if (IsZeroDWordFlag(format, "CanIncludeInClipboardHistory")
                || IsZeroDWordFlag(format, "CanUploadToCloudClipboard"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIgnoreByFormatName(string formatName) =>
        formatName.Equals("Clipboard Viewer Ignore", StringComparison.OrdinalIgnoreCase)
        || formatName.Equals("ExcludeClipboardContentFromMonitorProcessing", StringComparison.OrdinalIgnoreCase);

    private static bool IsZeroDWordFlag(RawFormatData format, string formatName) =>
        format.FormatName.Equals(formatName, StringComparison.OrdinalIgnoreCase)
        && format.Data.AsSpan().SequenceEqual(ZeroDWord);
}
