using ClipStream.Core.Models;

namespace ClipStream.Plugins.Abstractions;

public interface IFragmentPreviewPlugin : IClipStreamPlugin
{
    bool CanPreview(ClipboardFragment fragment);

    Task<FragmentPreviewResult?> BuildPreviewAsync(
        ClipboardFragment fragment,
        FragmentPreviewContext context,
        CancellationToken cancellationToken = default);
}
