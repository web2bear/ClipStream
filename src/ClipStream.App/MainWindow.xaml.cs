using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        if (menu.PlacementTarget is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is ClipboardFragment fragment)
        {
            viewModel.SelectedFragment = fragment;
            await viewModel.RefreshFragmentContextMenuAsync(fragment);
        }
    }

    private async void FragmentsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.ExecuteFragmentActionByIdAsync("builtin.action.paste", viewModel.SelectedFragment);
        }
    }

    private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }

    private async void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.DataContext is not ClipboardFragment fragment) return;
        if (DataContext is not ViewModels.MainViewModel viewModel) return;

        var newTitle = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newTitle) || newTitle == fragment.Title)
        {
            return;
        }

        await viewModel.SaveFragmentTitleAsync(fragment, newTitle);
    }
}
