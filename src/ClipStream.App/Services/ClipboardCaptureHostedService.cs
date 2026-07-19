using ClipStream.Clipboard.Capture;
using ClipStream.Clipboard.Listener;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Plugins.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipStream.App.Services;

public sealed class ClipboardCaptureHostedService : IHostedService
{
    private readonly IClipboardListener _listener;
    private readonly IClipboardCaptureService _captureService;
    private readonly IPluginPipeline _pipeline;
    private readonly IRoutingEngine _routingEngine;
    private readonly IFragmentRepository _fragmentRepository;
    private readonly ClipStreamDatabase _database;
    private readonly IStreamRepository _streamRepository;
    private readonly IPluginLoader _pluginLoader;
    private readonly ILogger<ClipboardCaptureHostedService> _logger;

    public ClipboardCaptureHostedService(
        IClipboardListener listener,
        IClipboardCaptureService captureService,
        IPluginPipeline pipeline,
        IRoutingEngine routingEngine,
        IFragmentRepository fragmentRepository,
        ClipStreamDatabase database,
        IStreamRepository streamRepository,
        IPluginLoader pluginLoader,
        ILogger<ClipboardCaptureHostedService> logger)
    {
        _listener = listener;
        _captureService = captureService;
        _pipeline = pipeline;
        _routingEngine = routingEngine;
        _fragmentRepository = fragmentRepository;
        _database = database;
        _streamRepository = streamRepository;
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _database.InitializeAsync(cancellationToken);
        await _streamRepository.EnsureDefaultStreamAsync(cancellationToken);
        _listener.ClipboardChanged += OnClipboardChanged;
        _logger.LogInformation(
            "Clipboard capture service started. Plugins: {Count}, HWND: {Hwnd}",
            _pluginLoader.FormatPlugins.Count,
            _listener.ListenerHwnd);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _listener.ClipboardChanged -= OnClipboardChanged;
        return Task.CompletedTask;
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        _ = ProcessClipboardChangeAsync();
    }

    private async Task ProcessClipboardChangeAsync()
    {
        try
        {
            if (_pluginLoader.FormatPlugins.Count == 0)
            {
                _logger.LogWarning("No format plugins loaded; skipping clipboard capture");
                return;
            }

            var capture = await _captureService.CaptureAsync();
            if (capture is null || capture.Formats.Count == 0)
            {
                _logger.LogWarning("Clipboard capture returned no formats");
                return;
            }

            if (ClipboardPrivacyFilter.ShouldIgnore(capture))
            {
                _logger.LogDebug(
                    "Clipboard change ignored (privacy/exclude format), sequence {Sequence}",
                    capture.ClipboardSequence);
                return;
            }

            var fragment = await _pipeline.ProcessAsync(capture);
            if (fragment is null)
            {
                _logger.LogDebug("Clipboard change ignored (duplicate or unsupported)");
                return;
            }

            var streamId = await _routingEngine.RouteAsync(fragment);
            await _fragmentRepository.SaveAsync(fragment, streamId);
            _logger.LogInformation(
                "Captured fragment {FragmentId} ({Kind}) -> stream {StreamId}",
                fragment.Id,
                fragment.Kind,
                streamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process clipboard change");
        }
    }
}
