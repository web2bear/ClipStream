using ClipStream.Clipboard.Paste;
using ClipStream.Core.Models;
using ClipStream.Core.Paste;

namespace ClipStream.Clipboard.Paste;

public sealed class FragmentPasteService : IFragmentPasteService
{
    private readonly IClipboardWriter _clipboardWriter;

    public FragmentPasteService(IClipboardWriter clipboardWriter) => _clipboardWriter = clipboardWriter;

    public Task PasteToActiveWindowAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default) =>
        _clipboardWriter.PasteFragmentToActiveWindowAsync(fragment, cancellationToken);
}
