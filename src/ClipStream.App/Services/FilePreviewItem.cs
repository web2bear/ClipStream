using System.Windows.Media;

namespace ClipStream.App.Services;

public sealed record FilePreviewItem(
    string Path,
    string DisplayName,
    string ActionLabel,
    bool IsExecutable,
    bool Exists,
    ImageSource? Icon);
