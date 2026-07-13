using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using ClipStream.Clipboard.Capture;
using ClipStream.Clipboard.Listener;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ClipStream.Clipboard.Tests;

public class ClipboardCaptureIntegrationTests
{
    [Fact]
    public async Task CaptureService_ReadsText_FromClipboard()
    {
        await StaTest.RunAsync(async () =>
        {
            EnsureWpfApplication();
            var services = CreateMinimalServices();
            await using var scope = services.CreateAsyncScope();

            var captureService = scope.ServiceProvider.GetRequiredService<IClipboardCaptureService>();
            var uniqueText = $"clipstream-test-{Guid.NewGuid()}";
            System.Windows.Clipboard.SetText(uniqueText);

            var capture = await captureService.CaptureAsync();
            Assert.NotNull(capture);
            Assert.NotEmpty(capture!.Formats);

            var textFormat = capture.Formats.FirstOrDefault(f =>
                f.FormatName is "CF_UNICODETEXT" or "UnicodeText" or "Text" or "text/plain");
            Assert.NotNull(textFormat);
            Assert.Contains(uniqueText, System.Text.Encoding.Unicode.GetString(textFormat!.Data));
        });
    }

    [Fact]
    public async Task Pipeline_SavesFragment_FromClipboard()
    {
        await StaTest.RunAsync(async () =>
        {
            EnsureWpfApplication();
            var tempRoot = Path.Combine(Path.GetTempPath(), "clipstream-it-" + Guid.NewGuid());
            var services = TestServiceFactory.Create(tempRoot);
            await using var scope = services.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            var database = sp.GetRequiredService<ClipStreamDatabase>();
            await database.InitializeAsync();
            await sp.GetRequiredService<IStreamRepository>().EnsureDefaultStreamAsync();

            var captureService = sp.GetRequiredService<IClipboardCaptureService>();
            var pipeline = sp.GetRequiredService<IPluginPipeline>();
            var routingEngine = sp.GetRequiredService<IRoutingEngine>();
            var fragmentRepository = sp.GetRequiredService<IFragmentRepository>();

            var uniqueText = $"clipstream-e2e-{Guid.NewGuid()}";
            System.Windows.Clipboard.SetText(uniqueText);

            var capture = await captureService.CaptureAsync();
            Assert.NotNull(capture);
            Assert.NotEmpty(capture!.Formats);

            var fragment = await pipeline.ProcessAsync(capture);
            Assert.NotNull(fragment);

            var streamId = await routingEngine.RouteAsync(fragment!);
            await fragmentRepository.SaveAsync(fragment!, streamId);

            var loaded = await fragmentRepository.GetByIdAsync(fragment!.Id);
            Assert.NotNull(loaded);
            Assert.Equal(uniqueText, loaded!.PreviewText);

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempRoot, true);
        });
    }

    [Fact]
    public async Task Listener_RaisesEvent_WhenClipboardChanges()
    {
        await StaTest.RunAsync(async () =>
        {
            EnsureWpfApplication();
            var services = CreateMinimalServices();
            await using var scope = services.CreateAsyncScope();

            var listener = scope.ServiceProvider.GetRequiredService<IClipboardListener>();
            var hostWindow = CreateListenerWindow(out var dispatcher);
            try
            {
                listener.Initialize(hostWindow.Handle);
                Assert.NotEqual(IntPtr.Zero, listener.ListenerHwnd);

                var eventTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                listener.ClipboardChanged += (_, _) => eventTcs.TrySetResult();

                System.Windows.Clipboard.SetText($"listener-test-{Guid.NewGuid()}");

                await ClipboardMessagePump.WaitUntilAsync(dispatcher, () => eventTcs.Task.IsCompleted, TimeSpan.FromSeconds(3));
            }
            finally
            {
                hostWindow.Close();
            }
        });
    }

    private static ServiceProvider CreateMinimalServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddClipStreamClipboard();
        return services.BuildServiceProvider();
    }

    private static void EnsureWpfApplication()
    {
        if (Application.Current is null)
        {
            _ = new Application();
        }
    }

    private static ListenerHostWindow CreateListenerWindow(out Dispatcher dispatcher)
    {
        var window = new ListenerHostWindow();
        window.Show();
        window.WaitForHandle();
        dispatcher = window.Dispatcher;
        return window;
    }

    private sealed class ListenerHostWindow : Window
    {
        private readonly TaskCompletionSource _handleReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IntPtr Handle { get; private set; }

        public ListenerHostWindow()
        {
            Width = 1;
            Height = 1;
            Left = -10000;
            Top = -10000;
            WindowStyle = WindowStyle.None;
            ShowInTaskbar = false;
            ShowActivated = false;
            Visibility = Visibility.Hidden;
            SourceInitialized += (_, _) =>
            {
                Handle = new WindowInteropHelper(this).Handle;
                _handleReady.TrySetResult();
            };
        }

        public void WaitForHandle() => _handleReady.Task.GetAwaiter().GetResult();
    }
}
