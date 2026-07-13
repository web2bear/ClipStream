using System.Windows;
using System.Windows.Controls;
using ClipStream.App.ViewModels;
using ClipStream.Core.Models;

namespace ClipStream.App.Behaviors;

public static class StreamDropBehavior
{
    private static System.Windows.Media.Brush GetDropHighlightBrush() =>
        System.Windows.Application.Current.TryFindResource("DropHighlightBrush") as System.Windows.Media.Brush
        ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0x35, 0x68));

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(StreamDropBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBoxItem item)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            item.AllowDrop = true;
            item.DragOver += OnDragOver;
            item.DragEnter += OnDragEnter;
            item.DragLeave += OnDragLeave;
            item.Drop += OnDrop;
        }
        else
        {
            item.AllowDrop = false;
            item.DragOver -= OnDragOver;
            item.DragEnter -= OnDragEnter;
            item.DragLeave -= OnDragLeave;
            item.Drop -= OnDrop;
        }
    }

    private static void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ListBoxItem item && CanAcceptDrop(item, e))
        {
            item.Background = GetDropHighlightBrush();
        }
    }

    private static void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not ListBoxItem item)
        {
            return;
        }

        if (CanAcceptDrop(item, e))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private static void OnDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
        }
    }

    private static async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is not ListBoxItem item)
        {
            return;
        }

        item.ClearValue(System.Windows.Controls.Control.BackgroundProperty);

        if (!CanAcceptDrop(item, e)
            || item.DataContext is not ClipStreamEntity targetStream
            || !e.Data.GetDataPresent(FragmentDragBehavior.FragmentIdFormat))
        {
            return;
        }

        var fragmentId = (Guid)e.Data.GetData(FragmentDragBehavior.FragmentIdFormat)!;
        var viewModel = Window.GetWindow(item)?.DataContext as MainViewModel;
        if (viewModel is null)
        {
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
        await viewModel.MoveFragmentToStreamAsync(fragmentId, targetStream.Id);
    }

    private static bool CanAcceptDrop(ListBoxItem item, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(FragmentDragBehavior.FragmentIdFormat)
            || item.DataContext is not ClipStreamEntity targetStream)
        {
            return false;
        }

        if (e.Data.GetDataPresent(FragmentDragBehavior.SourceStreamIdFormat)
            && e.Data.GetData(FragmentDragBehavior.SourceStreamIdFormat) is Guid sourceStreamId
            && sourceStreamId == targetStream.Id)
        {
            return false;
        }

        return true;
    }
}
