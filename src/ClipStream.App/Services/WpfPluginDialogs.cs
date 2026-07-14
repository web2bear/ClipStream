using System.Windows.Threading;
using ClipStream.Plugins.Abstractions;
using Microsoft.Win32;

namespace ClipStream.App.Services;

public sealed class WpfPluginDialogs : IPluginDialogs
{
    public Task<string?> PickFolderAsync(string description, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // ContextIdle specifically waits for open context menus/popups to finish closing
        // before running. Showing a modal dialog while the context menu popup still has
        // mouse capture can prevent the dialog from becoming visible/interactive.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            () =>
            {
                try
                {
                    tcs.SetResult(ShowFolderDialog(description));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

        return tcs.Task;
    }

    private static string? ShowFolderDialog(string description)
    {
        var dialog = new OpenFolderDialog
        {
            Title = string.IsNullOrWhiteSpace(description) ? "Выберите папку для сохранения" : description,
            Multiselect = false
        };

        var owner = System.Windows.Application.Current.MainWindow;
        var result = owner is { IsVisible: true }
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();

        return result == true ? dialog.FolderName : null;
    }
}
