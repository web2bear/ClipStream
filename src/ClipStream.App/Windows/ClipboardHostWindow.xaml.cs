using System.Windows;
using ClipStream.Clipboard.Listener;

namespace ClipStream.App.Windows;

public partial class ClipboardHostWindow : Window
{
    private readonly IClipboardListener _clipboardListener;

    public ClipboardHostWindow(IClipboardListener clipboardListener)
    {
        _clipboardListener = clipboardListener;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        _clipboardListener.Initialize(helper.Handle);
    }
}
