using System.Windows.Input;

using ClipStream.App.Services;

using ClipStream.Core.Models;

using ClipStream.Plugins.Abstractions;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;



namespace ClipStream.App.ViewModels;



public partial class StreamActionMenuItemViewModel : ObservableObject, IContextMenuItemViewModel

{

    private readonly IStreamActionPlugin _plugin;

    private readonly Func<ClipStreamEntity?, StreamActionContext?> _createContext;

    private readonly IStatusReporter _statusReporter;



    public StreamActionMenuItemViewModel(

        IStreamActionPlugin plugin,

        Func<ClipStreamEntity?, StreamActionContext?> createContext,

        IStatusReporter statusReporter)

    {

        _plugin = plugin;

        _createContext = createContext;

        _statusReporter = statusReporter;

        ExecuteCommand = new RelayCommand<ClipStreamEntity?>(
            stream => _ = ExecuteAsync(stream),
            _ => IsEnabled);

    }



    public string Header => _plugin.MenuTitle;



    public string? MenuGroup => _plugin.MenuGroup;



    public int MenuOrder => _plugin.MenuOrder;



    public string PluginId => _plugin.Descriptor.Id;



    [ObservableProperty]

    private bool _isEnabled = true;



    public RelayCommand<ClipStreamEntity?> ExecuteCommand { get; }



    public ICommand Command => ExecuteCommand;



    public async Task UpdateCanExecuteAsync(ClipStreamEntity? stream)

    {

        var context = _createContext(stream);

        IsEnabled = context is not null && await _plugin.CanExecuteAsync(context);

        ExecuteCommand.NotifyCanExecuteChanged();

    }



    partial void OnIsEnabledChanged(bool value) => ExecuteCommand.NotifyCanExecuteChanged();



    private async Task ExecuteAsync(ClipStreamEntity? stream)

    {

        try

        {

            var context = _createContext(stream);

            if (context is null)

            {

                _statusReporter.ReportStatus("Action is not available for this stream");

                return;

            }



            if (!await _plugin.CanExecuteAsync(context))

            {

                _statusReporter.ReportStatus("Action is not available for this stream");

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


