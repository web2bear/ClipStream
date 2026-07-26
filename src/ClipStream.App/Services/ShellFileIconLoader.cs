using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClipStream.App.Services;

internal static class ShellFileIconLoader
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeDirectory = 0x00000010;

    public static ImageSource? GetIcon(string path, bool exists, bool isDirectory)
    {
        var flags = ShgfiIcon | ShgfiSmallIcon;
        var attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
        if (!exists)
        {
            flags |= ShgfiUseFileAttributes;
        }

        var info = new ShFileInfo();
        var result = SHGetFileInfo(
            path,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<ShFileInfo>(),
            flags);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
