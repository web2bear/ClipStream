using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipStream.Core.Models;

public sealed record ClipboardFragment(
    Guid Id,
    DateTimeOffset CapturedAt,
    FragmentKind Kind,
    string? PreviewText,
    string? SourceProcessName,
    int? SourceProcessId,
    IReadOnlyList<FormatPayload> Payloads,
    IReadOnlyDictionary<string, string> Metadata,
    string? ContentHash = null) : INotifyPropertyChanged
{
    private string? _title;

    public string Title
    {
        get => _title ??= CreateDefaultTitle();
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// WPF selectors break when list items use value equality and raise PropertyChanged.
    /// Keep identity-based equality so SelectedItem stays stable across title edits.
    /// </summary>
    public bool Equals(ClipboardFragment? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    private string CreateDefaultTitle() => Kind switch
    {
        FragmentKind.Text or FragmentKind.RichText when PreviewText is { Length: > 0 } =>
            PreviewText.Length > 128 ? PreviewText[..128] : PreviewText,
        FragmentKind.Image => $"Изображение от {CapturedAt:yyyy-MM-dd HH:mm}",
        FragmentKind.Files => $"Файлы от {CapturedAt:yyyy-MM-dd HH:mm}",
        FragmentKind.Binary => $"Двоичные данные от {CapturedAt:yyyy-MM-dd HH:mm}",
        FragmentKind.Composite => $"Составной фрагмент от {CapturedAt:yyyy-MM-dd HH:mm}",
        _ => $"Фрагмент от {CapturedAt:yyyy-MM-dd HH:mm}"
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
