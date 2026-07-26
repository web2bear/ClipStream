using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipStream.App.ViewModels;

public partial class DeleteFragmentContextMenuItemViewModel : ObservableObject, IContextMenuItemViewModel
{
    private readonly MainViewModel _viewModel;

    public DeleteFragmentContextMenuItemViewModel(MainViewModel viewModel) => _viewModel = viewModel;

    public string Header => "Удалить";

    public ICommand Command => _viewModel.DeleteFragmentCommand;

    public bool IsEnabled => _viewModel.DeleteFragmentCommand.CanExecute(_viewModel.SelectedFragment);

    public void Refresh() => OnPropertyChanged(nameof(IsEnabled));
}
