namespace ClipStream.Clipboard.Paste;

public interface IPasteUiHook
{
    void OnBeforeExternalPaste();
}

public sealed class NullPasteUiHook : IPasteUiHook
{
    public static NullPasteUiHook Instance { get; } = new();

    public void OnBeforeExternalPaste()
    {
    }
}
