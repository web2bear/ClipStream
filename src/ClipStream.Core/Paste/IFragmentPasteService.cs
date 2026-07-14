using ClipStream.Core.Models;

namespace ClipStream.Core.Paste;

public interface IFragmentPasteService
{
    Task PasteToActiveWindowAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);
}
