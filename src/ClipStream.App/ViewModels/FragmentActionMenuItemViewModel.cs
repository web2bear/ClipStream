using System.Windows.Input;

using ClipStream.App.Services;

using ClipStream.Core.Models;

using ClipStream.Plugins.Abstractions;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;



namespace ClipStream.App.ViewModels;



public partial class FragmentActionMenuItemViewModel : ObservableObject, IContextMenuItemViewModel

{

    private readonly IFragmentActionPlugin _plugin;

    private readonly Func<ClipboardFragment?, FragmentActionContext?> _createContext;

    private readonly IStatusReporter _statusReporter;



    public FragmentActionMenuItemViewModel(

        IFragmentActionPlugin plugin,

        Func<ClipboardFragment?, FragmentActionContext?> createContext,

        IStatusReporter statusReporter)

    {

        _plugin = plugin;

        _createContext = createContext;

        _statusReporter = statusReporter;

        ExecuteCommand = new RelayCommand<ClipboardFragment?>(
            fragment => _ = ExecuteAsync(fragment),
            _ => IsEnabled);

    }



    public string Header => _plugin.MenuTitle;



    public string? MenuGroup => _plugin.MenuGroup;



    public int MenuOrder => _plugin.MenuOrder;



    public string PluginId => _plugin.Descriptor.Id;



    [ObservableProperty]

    private bool _isEnabled = true;



    public RelayCommand<ClipboardFragment?> ExecuteCommand { get; }



    public ICommand Command => ExecuteCommand;



    public async Task UpdateCanExecuteAsync(ClipboardFragment? fragment)

    {

        var context = _createContext(fragment);

        IsEnabled = context is not null && await _plugin.CanExecuteAsync(context);

        ExecuteCommand.NotifyCanExecuteChanged();

    }



    partial void OnIsEnabledChanged(bool value) => ExecuteCommand.NotifyCanExecuteChanged();



    private async Task ExecuteAsync(ClipboardFragment? fragment)

    {

        try

        {

            var context = _createContext(fragment);

            if (context is null)

            {

                _statusReporter.ReportStatus("Action is not available for this fragment");

                return;

            }



            if (!await _plugin.CanExecuteAsync(context))

            {

                _statusReporter.ReportStatus("Action is not available for this fragment");

                return;

            }



            await _plugin.ExecuteAsync(context);

        }

        catch (Exception ex)

        {

            _statusReporter.ReportStatus($"Action failed: {ex.Message}");

        }

    }

}


