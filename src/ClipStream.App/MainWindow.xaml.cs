using System.Windows;
using System.Windows.Input;

namespace ClipStream.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void FragmentsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel
            && viewModel.PasteFragmentCommand.CanExecute(viewModel.SelectedFragment))
        {
            await viewModel.PasteFragmentCommand.ExecuteAsync(viewModel.SelectedFragment);
        }
    }
}
