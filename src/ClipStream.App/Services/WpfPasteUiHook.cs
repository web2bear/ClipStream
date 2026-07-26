using ClipStream.Clipboard.Paste;

namespace ClipStream.App.Services;

public sealed class WpfPasteUiHook : IPasteUiHook
{
    public void OnBeforeExternalPaste()
    {
        if (System.Windows.Application.Current?.MainWindow is { IsVisible: true } mainWindow)
        {
            mainWindow.Hide();
        }
    }
}
