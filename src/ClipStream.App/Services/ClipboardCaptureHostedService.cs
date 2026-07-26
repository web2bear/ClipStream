using System.Threading.Channels;
using ClipStream.Clipboard.Capture;
using ClipStream.Clipboard.Listener;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
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
    private readonly IPluginLoader _pluginLoader;
    private readonly ILogger<ClipboardCaptureHostedService> _logger;
    private readonly Channel<uint> _pendingSequences = Channel.CreateBounded<uint>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;

    public ClipboardCaptureHostedService(
        IClipboardListener listener,
        IClipboardCaptureService captureService,
        IPluginPipeline pipeline,
        IRoutingEngine routingEngine,
        IFragmentRepository fragmentRepository,
        IPluginLoader pluginLoader,
        ILogger<ClipboardCaptureHostedService> logger)
    {
        _listener = listener;
        _captureService = captureService;
        _pipeline = pipeline;
        _routingEngine = routingEngine;
        _fragmentRepository = fragmentRepository;
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.ClipboardChanged += OnClipboardChanged;
        _workerTask = ProcessQueueAsync(_workerCts.Token);
        _logger.LogInformation(
            "Clipboard capture service started. Plugins: {Count}, HWND: {Hwnd}",
            _pluginLoader.FormatPlugins.Count,
            _listener.ListenerHwnd);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _listener.ClipboardChanged -= OnClipboardChanged;
        _pendingSequences.Writer.TryComplete();

        if (_workerCts is not null)
        {
            await _workerCts.CancelAsync();
        }

        if (_workerTask is not null)
        {
            try
            {
                await _workerTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _workerCts?.Dispose();
        _workerCts = null;
        _workerTask = null;
    }

    private void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        _pendingSequences.Writer.TryWrite(e.SequenceNumber);
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in _pendingSequences.Reader.ReadAllAsync(cancellationToken))
            {
                await ProcessClipboardChangeAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
    }

    private async Task ProcessClipboardChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_pluginLoader.FormatPlugins.Count == 0)
            {
                _logger.LogWarning("No format plugins loaded; skipping clipboard capture");
                return;
            }

            var capture = await _captureService.CaptureAsync(cancellationToken);
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

            var fragment = await _pipeline.ProcessAsync(capture, cancellationToken);
            if (fragment is null)
            {
                _logger.LogDebug("Clipboard change ignored (duplicate or unsupported)");
                return;
            }

            var streamId = await _routingEngine.RouteAsync(fragment, cancellationToken);
            await _fragmentRepository.SaveAsync(fragment, streamId, cancellationToken);
            _logger.LogInformation(
                "Captured fragment {FragmentId} ({Kind}) -> stream {StreamId}",
                fragment.Id,
                fragment.Kind,
                streamId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process clipboard change");
        }
    }
}
