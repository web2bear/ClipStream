using System.Globalization;
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
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public IServiceProvider Services => _host!.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Use system culture for date/number formatting throughout the app
        var culture = CultureInfo.GetCultureInfo(CultureInfo.InstalledUICulture.Name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // WPF uses Language property for binding formatting, not CurrentCulture
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

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
                services.AddSingleton<IPluginDialogs, WpfPluginDialogs>();
                services.AddSingleton<MutableStatusReporter>();
                services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<MutableStatusReporter>());
                services.AddSingleton<IPluginHost, PluginHost>();
                services.AddSingleton<ActionContextFactory>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IFragmentPreviewService, FragmentPreviewService>();
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

        var pluginHost = Services.GetRequiredService<IPluginHost>();
        await pluginLoader.ActivateActionPluginsAsync(pluginHost);

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
        MainWindow = mainWindow;

        SetupTrayIcon(mainWindow);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_host is not null)
        {
            var pluginLoader = Services.GetRequiredService<IPluginLoader>();
            await pluginLoader.DeactivateActionPluginsAsync();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void SetupTrayIcon(System.Windows.Window mainWindow)
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ClipStream",
            Icon = LoadAppIcon(),
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => RestoreMainWindow(mainWindow);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => RestoreMainWindow(mainWindow));
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _trayIcon.ContextMenuStrip = menu;

        mainWindow.StateChanged += (_, _) =>
        {
            if (mainWindow.WindowState != System.Windows.WindowState.Minimized)
            {
                return;
            }

            mainWindow.Hide();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
        };
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
        var streamInfo = GetResourceStream(uri);
        if (streamInfo?.Stream is not null)
        {
            using var stream = streamInfo.Stream;
            // NotifyIcon keeps the icon handle; clone so the stream can be disposed.
            using var temp = new System.Drawing.Icon(stream);
            return (System.Drawing.Icon)temp.Clone();
        }

        return System.Drawing.SystemIcons.Application;
    }

    private static void RestoreMainWindow(System.Windows.Window mainWindow)
    {
        mainWindow.Show();
        mainWindow.WindowState = System.Windows.WindowState.Normal;
        mainWindow.Activate();
    }
}
