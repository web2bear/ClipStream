using System.Windows;
using ClipStream.Core.Models;

namespace ClipStream.App.Windows;

public partial class StreamDialog : Window
{
    public StreamDialogResult? Result { get; private set; }

    private StreamDialog(bool isEdit, string defaultName, string defaultIcon)
    {
        InitializeComponent();
        Title = isEdit ? "Edit stream" : "New stream";
        ConfirmButton.Content = isEdit ? "Save" : "Create";
        NameBox.Text = defaultName;
        IconList.ItemsSource = StreamIcons.All;
        IconList.SelectedItem = StreamIcons.All.FirstOrDefault(icon =>
            string.Equals(icon.Key, defaultIcon, StringComparison.OrdinalIgnoreCase))
            ?? StreamIcons.All.First(icon => icon.Key == StreamIcons.DefaultKey);

        Loaded += (_, _) =>
        {
            CenterOnOwner();
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public static StreamDialogResult? ShowCreate(Window owner) =>
        Show(owner, isEdit: false, defaultName: string.Empty, defaultIcon: StreamIcons.DefaultKey);

    public static StreamDialogResult? ShowEdit(Window owner, ClipStreamEntity stream) =>
        Show(owner, isEdit: true, stream.Name, stream.Icon ?? StreamIcons.DefaultKey);

    private static StreamDialogResult? Show(Window owner, bool isEdit, string defaultName, string defaultIcon)
    {
        var dialog = new StreamDialog(isEdit, defaultName, defaultIcon)
        {
            Owner = owner,
        };

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void CenterOnOwner()
    {
        if (Owner is not Window owner)
        {
            return;
        }

        UpdateLayout();
        Left = owner.Left + (owner.ActualWidth - ActualWidth) / 2;
        Top = owner.Top + (owner.ActualHeight - ActualHeight) / 2;
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var icon = (IconList.SelectedItem as StreamIcons.IconOption)?.Key ?? StreamIcons.DefaultKey;
        Result = new StreamDialogResult(name, icon);
        DialogResult = true;
    }
}

public sealed record StreamDialogResult(string Name, string Icon);
