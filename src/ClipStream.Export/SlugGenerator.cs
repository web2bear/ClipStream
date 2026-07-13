using System.Text;
using System.Text.RegularExpressions;

namespace ClipStream.Export;

public static class SlugGenerator
{
    private static readonly Regex InvalidChars = new(@"[^\w\-]+", RegexOptions.Compiled);

    public static string FromText(string? text, int maxLength = 40)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "fragment";
        }

        var slug = text.Trim().ToLowerInvariant();
        slug = InvalidChars.Replace(slug, "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return string.IsNullOrEmpty(slug) ? "fragment" : slug;
    }

    public static string FromStreamName(string name) => FromText(name, 60);
}
