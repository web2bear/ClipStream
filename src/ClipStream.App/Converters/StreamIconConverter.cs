using System.Globalization;
using System.Windows.Data;

namespace ClipStream.App.Converters;

public sealed class StreamIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        StreamIcons.GetGlyph(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
