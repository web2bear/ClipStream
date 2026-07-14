using System.Windows.Input;

namespace ClipStream.App.ViewModels;

public interface IContextMenuItemViewModel
{
    string Header { get; }

    ICommand Command { get; }

    bool IsEnabled { get; }
}
