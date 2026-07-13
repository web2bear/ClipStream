using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipStream.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ClipStream.App.Behaviors;

public static class FragmentDragBehavior
{
    public const string FragmentIdFormat = "ClipStream.FragmentId";
    public const string SourceStreamIdFormat = "ClipStream.SourceStreamId";

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FragmentDragBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty FragmentProperty =
        DependencyProperty.RegisterAttached(
            "Fragment",
            typeof(ClipboardFragment),
            typeof(FragmentDragBehavior),
            new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static ClipboardFragment? GetFragment(DependencyObject obj) => (ClipboardFragment?)obj.GetValue(FragmentProperty);

    public static void SetFragment(DependencyObject obj, ClipboardFragment? value) => obj.SetValue(FragmentProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            element.MouseMove += OnMouseMove;
            element.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        }
        else
        {
            element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            element.MouseMove -= OnMouseMove;
            element.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
        }
    }

    private static System.Windows.Point? _startPoint;
    private static bool _isDragging;

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private static async void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _startPoint is null || sender is not FrameworkElement element)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _startPoint.Value.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _startPoint.Value.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (_isDragging)
        {
            return;
        }

        _isDragging = true;
        var fragment = GetFragment(element);
        if (fragment is null)
        {
            return;
        }

        var app = System.Windows.Application.Current as App;
        var payloadBuilder = app?.Services.GetService(typeof(Clipboard.Paste.IClipboardPayloadBuilder)) as Clipboard.Paste.IClipboardPayloadBuilder;
        if (payloadBuilder is null)
        {
            return;
        }

        var dataObject = await payloadBuilder.BuildDataObjectAsync(fragment);
        dataObject.SetData(FragmentIdFormat, fragment.Id);

        if (System.Windows.Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel viewModel
            && viewModel.SelectedStream is { } sourceStream)
        {
            dataObject.SetData(SourceStreamIdFormat, sourceStream.Id);
        }

        System.Windows.DragDrop.DoDragDrop(
            element,
            dataObject,
            System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
        _isDragging = false;
        _startPoint = null;
    }

    private static void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _startPoint = null;
        _isDragging = false;
    }
}
