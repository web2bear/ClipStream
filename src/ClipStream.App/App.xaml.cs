using System.Windows;
using ClipStream.App.Services;
using ClipStream.App.ViewModels;
using ClipStream.App.Windows;
using ClipStream.Clipboard;
using ClipStream.Export;
using ClipStream.Infrastructure;
using ClipStream.Plugins.Abstractions;
using ClipStream.Plugins.BuiltIn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipStream.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public IServiceProvider Services => _host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices(services =>
            {
                services.AddClipStreamInfrastructure();
                services.AddClipStreamClipboard();
                services.AddClipStreamExport();
                services.AddBuiltInPlugins();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<ClipboardHostWindow>();
                services.AddHostedService<ClipboardCaptureHostedService>();
            })
            .Build();

        Services.GetRequiredService<IThemeService>().LoadSavedTheme();

        var pluginLoader = Services.GetRequiredService<IPluginLoader>();
        await pluginLoader.ReloadAsync();
        ClipStream.Plugins.BuiltIn.ServiceCollectionExtensions.RegisterBuiltInPluginsWithLoader(Services);

        var hostWindow = Services.GetRequiredService<ClipboardHostWindow>();
        var listenerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hostWindow.SourceInitialized += (_, _) => listenerReady.TrySetResult();
        hostWindow.Show();

        await listenerReady.Task;
        await _host.StartAsync();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        var viewModel = Services.GetRequiredService<MainViewModel>();
        await viewModel.InitializeAsync();
        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        SetupTrayIcon(mainWindow);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void SetupTrayIcon(System.Windows.Window mainWindow)
    {
        var tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "ClipStream",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };

        tray.DoubleClick += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Activate();
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) =>
        {
            mainWindow.Show();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Activate();
        });
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;

        mainWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            mainWindow.Hide();
        };
    }
}
