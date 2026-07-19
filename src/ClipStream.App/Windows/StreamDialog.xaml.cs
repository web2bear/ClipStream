using System.Windows;
using System.Windows.Controls;
using ClipStream.Core.Models;

namespace ClipStream.App.Windows;

public partial class StreamDialog : Window
{
    private StreamIcons.IconOption _selectedIcon;

    public StreamDialogResult? Result { get; private set; }

    private StreamDialog(bool isEdit, string defaultName, string defaultIcon)
    {
        InitializeComponent();
        Title = isEdit ? "Изменить поток" : "Новый поток";
        ConfirmButton.Content = isEdit ? "Сохранить" : "Создать";
        NameBox.Text = defaultName;

        IconList.ItemsSource = StreamIcons.All;
        _selectedIcon = StreamIcons.All.FirstOrDefault(icon =>
            string.Equals(icon.Key, defaultIcon, StringComparison.OrdinalIgnoreCase))
            ?? StreamIcons.All.First(icon => icon.Key == StreamIcons.DefaultKey);
        IconList.SelectedItem = _selectedIcon;
        ApplySelectedIcon();

        Loaded += (_, _) =>
        {
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

    private void IconButton_OnClick(object sender, RoutedEventArgs e)
    {
        IconPopup.IsOpen = !IconPopup.IsOpen;
    }

    private void IconList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IconList.SelectedItem is not StreamIcons.IconOption icon)
        {
            return;
        }

        _selectedIcon = icon;
        ApplySelectedIcon();
        IconPopup.IsOpen = false;
    }

    private void ApplySelectedIcon()
    {
        SelectedIconGlyph.Text = _selectedIcon.Glyph;
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        Result = new StreamDialogResult(name, _selectedIcon.Key);
        DialogResult = true;
    }
}

public sealed record StreamDialogResult(string Name, string Icon);
