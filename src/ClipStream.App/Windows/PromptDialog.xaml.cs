using System.Windows;

namespace ClipStream.App.Windows;

public partial class PromptDialog : Window
{
    public string? Result { get; private set; }

    public PromptDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public static string? Show(Window owner, string title, string prompt, string defaultValue = "")
    {
        var dialog = new PromptDialog
        {
            Owner = owner,
            Title = title,
        };
        dialog.PromptText.Text = prompt;
        dialog.InputBox.Text = defaultValue;

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void CreateButton_OnClick(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text.Trim();
        DialogResult = true;
    }
}
