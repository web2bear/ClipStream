namespace ClipStream.Core.Models;

public static class FragmentKindExtensions
{
    public static bool IsTextKind(this FragmentKind kind) =>
        kind is FragmentKind.Text or FragmentKind.RichText;
}
