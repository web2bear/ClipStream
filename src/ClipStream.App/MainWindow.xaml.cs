using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipStream.Core.Models;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace ClipStream.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void StreamContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel || sender is not ContextMenu menu)
        {
            return;
        }

        if (menu.PlacementTarget is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is ClipStreamEntity stream)
        {
            viewModel.SelectedStream = stream;
            await viewModel.RefreshStreamContextMenuAsync(stream);
        }
    }

    private async void FragmentContextMenu_OnOpened(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.MainViewModel viewModel || sender is not ContextMenu menu)
        {
            return;
        }

        if (menu.PlacementTarget is System.Windows.Controls.ListView listView
            && listView.SelectedItem is ClipboardFragment fragment)
        {
            viewModel.SelectedFragment = fragment;
            await viewModel.RefreshFragmentContextMenuAsync(fragment);
        }
    }

    private void TitleDisplay_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || sender is not TextBlock display || display.Parent is not System.Windows.Controls.Panel panel)
        {
            return;
        }

        e.Handled = true;
        var editor = FindChild<TextBox>(panel);
        if (editor is null)
        {
            return;
        }

        display.Visibility = Visibility.Collapsed;
        editor.Visibility = Visibility.Visible;
        editor.Focus();
        editor.SelectAll();
    }

    private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            EndTitleEdit(textBox, commit: true);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            EndTitleEdit(textBox, commit: false);
        }
    }

    private async void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (textBox.DataContext is ClipboardFragment fragment
            && DataContext is ViewModels.MainViewModel viewModel)
        {
            var newTitle = textBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != fragment.Title)
            {
                await viewModel.SaveFragmentTitleAsync(fragment, newTitle);
            }
            else
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }
        }

        EndTitleEdit(textBox, commit: false);
    }

    private static void EndTitleEdit(TextBox editor, bool commit)
    {
        if (editor.Parent is not System.Windows.Controls.Panel panel)
        {
            return;
        }

        var display = FindChild<TextBlock>(panel);
        editor.Visibility = Visibility.Collapsed;
        if (display is not null)
        {
            display.Visibility = Visibility.Visible;
        }

        if (commit)
        {
            editor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private void PreviewImage_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            element.Focus();
        }
    }

    private void PreviewImage_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (DataContext is not ViewModels.MainViewModel viewModel || viewModel.PreviewImage is null)
        {
            return;
        }

        try
        {
            if (viewModel.PreviewImage is BitmapSource bitmap)
            {
                System.Windows.Clipboard.SetImage(bitmap);
                viewModel.StatusText = "Image copied to clipboard";
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            viewModel.StatusText = $"Copy failed: {ex.Message}";
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
