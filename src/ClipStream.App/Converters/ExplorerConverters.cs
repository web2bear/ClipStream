using System.Globalization;
using System.Windows.Data;
using ClipStream.Core.Models;

namespace ClipStream.App.Converters;

public sealed class ByteSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long bytes = value switch
        {
            long l => l,
            int i => i,
            IReadOnlyList<FormatPayload> payloads => payloads.Sum(p => p.SizeBytes),
            IEnumerable<FormatPayload> enumerable => enumerable.Sum(p => p.SizeBytes),
            _ => 0L
        };

        return FormatBytes(bytes);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} Б";
        }

        double kb = bytes / 1024.0;
        if (kb < 1024)
        {
            return $"{kb:0.##} КБ";
        }

        double mb = kb / 1024.0;
        if (mb < 1024)
        {
            return $"{mb:0.##} МБ";
        }

        return $"{mb / 1024.0:0.##} ГБ";
    }
}

public sealed class FragmentKindDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is FragmentKind kind ? Format(kind) : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public static string Format(FragmentKind kind) =>
        kind switch
        {
            FragmentKind.Text => "Текст",
            FragmentKind.RichText => "HTML / форматированный текст",
            FragmentKind.Image => "Изображение",
            FragmentKind.Files => "Файлы",
            FragmentKind.Binary => "Двоичные данные",
            FragmentKind.Composite => "Составной фрагмент",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}

public sealed class FragmentKindGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FragmentKind kind)
        {
            return "\uE8A5";
        }

        return kind switch
        {
            FragmentKind.Text => "\uE8C1",
            FragmentKind.RichText => "\uE8A5",
            FragmentKind.Image => "\uE91B",
            FragmentKind.Files => "\uE8B7",
            FragmentKind.Binary => "\uE8B7",
            FragmentKind.Composite => "\uE8B7",
            _ => throw new ArgumentOutOfRangeException(nameof(value), kind, null)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
