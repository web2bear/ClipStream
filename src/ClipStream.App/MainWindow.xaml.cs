using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClipStream.Core.Models;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace ClipStream.App;

public partial class MainWindow : Window
{
    private ClipboardFragment? _titleEditFragment;
    private bool _isEndingTitleEdit;

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
        if (e.ClickCount < 2
            || DataContext is not ViewModels.MainViewModel viewModel
            || viewModel.SelectedFragment is null)
        {
            return;
        }

        e.Handled = true;
        _titleEditFragment = viewModel.SelectedFragment;
        TitleDisplay.Visibility = Visibility.Collapsed;
        TitleEditor.Visibility = Visibility.Visible;
        TitleEditor.Focus();
        TitleEditor.SelectAll();
    }

    private async void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await FinishTitleEditAsync(save: true);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            await FinishTitleEditAsync(save: false);
        }
    }

    private async void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isEndingTitleEdit || _titleEditFragment is null)
        {
            return;
        }

        await FinishTitleEditAsync(save: true);
    }

    private async Task FinishTitleEditAsync(bool save)
    {
        if (_isEndingTitleEdit)
        {
            return;
        }

        _isEndingTitleEdit = true;
        try
        {
            var fragment = _titleEditFragment;
            _titleEditFragment = null;
            var newTitle = TitleEditor.Text.Trim();

            if (!save)
            {
                TitleEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            }

            TitleEditor.Visibility = Visibility.Collapsed;
            TitleDisplay.Visibility = Visibility.Visible;
            Keyboard.ClearFocus();

            if (save
                && fragment is not null
                && DataContext is ViewModels.MainViewModel viewModel
                && !string.IsNullOrWhiteSpace(newTitle)
                && newTitle != fragment.Title)
            {
                await viewModel.SaveFragmentTitleAsync(fragment, newTitle);
            }
        }
        finally
        {
            _isEndingTitleEdit = false;
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
}
