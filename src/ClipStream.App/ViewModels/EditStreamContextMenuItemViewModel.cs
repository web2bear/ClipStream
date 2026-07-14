using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipStream.App.ViewModels;

public partial class EditStreamContextMenuItemViewModel : ObservableObject, IContextMenuItemViewModel
{
    private readonly MainViewModel _viewModel;

    public EditStreamContextMenuItemViewModel(MainViewModel viewModel) => _viewModel = viewModel;

    public string Header => "Edit stream...";

    public ICommand Command => _viewModel.EditStreamCommand;

    public bool IsEnabled => _viewModel.EditStreamCommand.CanExecute(null);

    public void Refresh() => OnPropertyChanged(nameof(IsEnabled));
}
