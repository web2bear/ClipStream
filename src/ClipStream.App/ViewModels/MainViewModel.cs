using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ClipStream.App.Converters;
using ClipStream.App.Services;
using ClipStream.App.Windows;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Plugins.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipStream.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStreamRepository _streamRepository;
    private readonly IFragmentRepository _fragmentRepository;
    private readonly IPluginLoader _pluginLoader;
    private readonly ActionContextFactory _actionContextFactory;
    private readonly IThemeService _themeService;
    private readonly IFragmentPreviewService _previewService;
    private readonly MutableStatusReporter _statusReporter;
    private CancellationTokenSource? _previewLoadCts;
    private int _previewLoadVersion;

    [ObservableProperty]
    private ObservableCollection<ClipStreamEntity> _streams = [];

    [ObservableProperty]
    private ObservableCollection<ClipboardFragment> _fragments = [];

    [ObservableProperty]
    private ObservableCollection<IContextMenuItemViewModel> _fragmentContextMenuItems = [];

    [ObservableProperty]
    private ObservableCollection<IContextMenuItemViewModel> _streamContextMenuItems = [];

    [ObservableProperty]
    private ClipStreamEntity? _selectedStream;

    [ObservableProperty]
    private ClipboardFragment? _selectedFragment;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    private bool _isDetailsPaneVisible = true;

    [ObservableProperty]
    private GridLength _detailsPaneRowHeight = new(220);

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private bool _hasPreviewImage;

    [ObservableProperty]
    private string _selectedFragmentSizeText = string.Empty;

    [ObservableProperty]
    private string _selectedFragmentFormatsText = string.Empty;

    [ObservableProperty]
    private string _selectedFragmentKindText = string.Empty;

    [ObservableProperty]
    private bool _isTextEditorAvailable;

    [ObservableProperty]
    private IReadOnlyList<FilePreviewItem> _previewFiles = [];

    [ObservableProperty]
    private bool _hasPreviewFiles;

    public MainViewModel(
        IStreamRepository streamRepository,
        IFragmentRepository fragmentRepository,
        IPluginLoader pluginLoader,
        ActionContextFactory actionContextFactory,
        IThemeService themeService,
        IFragmentPreviewService previewService,
        MutableStatusReporter statusReporter)
    {
        _streamRepository = streamRepository;
        _fragmentRepository = fragmentRepository;
        _pluginLoader = pluginLoader;
        _actionContextFactory = actionContextFactory;
        _themeService = themeService;
        _previewService = previewService;
        _statusReporter = statusReporter;
        _statusReporter.SetHandler(message => StatusText = message);
        _isDarkTheme = themeService.IsDarkTheme;
        _themeService.ThemeChanged += (_, _) => IsDarkTheme = _themeService.IsDarkTheme;
        _fragmentRepository.FragmentAdded += OnFragmentAdded;
        _editStreamMenuItem = new EditStreamContextMenuItemViewModel(this);
        _deleteFragmentMenuItem = new DeleteFragmentContextMenuItemViewModel(this);
        BuildActionMenuItems();
    }

    public async Task InitializeAsync()
    {
        await LoadStreamsAsync();
        if (Streams.Count > 0)
        {
            SelectedStream = Streams[0];
        }
    }

    partial void OnSelectedStreamChanged(ClipStreamEntity? value)
    {
        EditStreamCommand.NotifyCanExecuteChanged();
        _ = UpdateStreamActionMenuItemsAsync(value);
        _ = LoadFragmentsAsync();
    }

    [RelayCommand]
    private async Task LoadStreamsAsync()
    {
        var streams = await _streamRepository.GetAllAsync();
        Streams = new ObservableCollection<ClipStreamEntity>(streams);
    }

    [RelayCommand]
    private async Task AddStreamAsync()
    {
        var owner = System.Windows.Application.Current.MainWindow;
        if (owner is null)
        {
            return;
        }

        var result = StreamDialog.ShowCreate(owner);
        if (result is null)
        {
            return;
        }

        var name = result.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existing = await _streamRepository.GetByNameAsync(name);
        if (existing is not null)
        {
            StatusText = $"Stream \"{name}\" already exists";
            return;
        }

        var sortOrder = Streams.Count > 0 ? Streams.Max(stream => stream.SortOrder) + 1 : 0;
        var stream = new ClipStreamEntity(Guid.NewGuid(), name, result.Icon, sortOrder, false);
        await _streamRepository.SaveAsync(stream);
        await LoadStreamsAsync();
        SelectedStream = Streams.FirstOrDefault(item => item.Id == stream.Id);
        StatusText = $"Created stream \"{name}\"";
    }

    private bool CanEditStream(ClipStreamEntity? stream) => (stream ?? SelectedStream) is not null;

    [RelayCommand(CanExecute = nameof(CanEditStream))]
    private async Task EditStreamAsync(ClipStreamEntity? stream)
    {
        stream ??= SelectedStream;
        if (stream is null)
        {
            return;
        }

        var owner = System.Windows.Application.Current.MainWindow;
        if (owner is null)
        {
            return;
        }

        var result = StreamDialog.ShowEdit(owner, stream);
        if (result is null)
        {
            return;
        }

        var name = result.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var existing = await _streamRepository.GetByNameAsync(name);
        if (existing is not null && existing.Id != stream.Id)
        {
            StatusText = $"Stream \"{name}\" already exists";
            return;
        }

        var updated = stream with { Name = name, Icon = result.Icon };
        await _streamRepository.SaveAsync(updated);

        var wasSelected = SelectedStream?.Id == stream.Id;
        await LoadStreamsAsync();
        if (wasSelected)
        {
            SelectedStream = Streams.FirstOrDefault(item => item.Id == stream.Id);
        }

        StatusText = $"Updated stream \"{name}\"";
    }

    [RelayCommand]
    private async Task LoadFragmentsAsync()
    {
        if (SelectedStream is null)
        {
            Fragments.Clear();
            return;
        }

        var fragments = await _fragmentRepository.GetByStreamAsync(SelectedStream.Id, 0, 200);
        Fragments = new ObservableCollection<ClipboardFragment>(fragments);
        StatusText = $"{Fragments.Count} fragments in {SelectedStream.Name}";
    }

    public async Task MoveFragmentToStreamAsync(Guid fragmentId, Guid targetStreamId)
    {
        var sourceStreamId = await _fragmentRepository.GetStreamIdForFragmentAsync(fragmentId);
        if (sourceStreamId is null || sourceStreamId == targetStreamId)
        {
            return;
        }

        await _fragmentRepository.MoveToStreamAsync(fragmentId, targetStreamId);

        if (SelectedStream?.Id == sourceStreamId)
        {
            var movedFragment = Fragments.FirstOrDefault(fragment => fragment.Id == fragmentId);
            if (movedFragment is not null)
            {
                Fragments.Remove(movedFragment);
            }

            if (SelectedFragment?.Id == fragmentId)
            {
                SelectedFragment = null;
            }
        }

        var targetStream = Streams.FirstOrDefault(stream => stream.Id == targetStreamId)
            ?? await _streamRepository.GetByIdAsync(targetStreamId);
        var targetName = targetStream?.Name ?? "stream";
        StatusText = $"Moved fragment to {targetName}";
    }

    private bool CanDeleteFragment(ClipboardFragment? fragment) => (fragment ?? SelectedFragment) is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteFragment))]
    private async Task DeleteFragmentAsync(ClipboardFragment? fragment)
    {
        fragment ??= SelectedFragment;
        if (fragment is null)
        {
            return;
        }

        try
        {
            await _fragmentRepository.DeleteAsync(fragment.Id);

            var inList = Fragments.FirstOrDefault(item => item.Id == fragment.Id);
            if (inList is not null)
            {
                Fragments.Remove(inList);
            }

            if (SelectedFragment?.Id == fragment.Id)
            {
                SelectedFragment = null;
            }

            StatusText = "Фрагмент удалён";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось удалить фрагмент: {ex.Message}";
        }
    }

    partial void OnSelectedFragmentChanged(ClipboardFragment? value)
    {
        _ = UpdateFragmentActionMenuItemsAsync(value);
        _ = LoadFragmentPreviewAsync(value);
    }

    [RelayCommand]
    private void ToggleTheme() => _themeService.ToggleTheme();

    [RelayCommand]
    private void ToggleDetailsPane()
    {
        IsDetailsPaneVisible = !IsDetailsPaneVisible;
        DetailsPaneRowHeight = IsDetailsPaneVisible ? new GridLength(220) : new GridLength(0);
    }

    [RelayCommand(CanExecute = nameof(CanOpenInTextEditor))]
    private async Task OpenInTextEditorAsync()
    {
        if (SelectedFragment is null || string.IsNullOrEmpty(PreviewText))
        {
            return;
        }

        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "ClipStream", "preview");
            Directory.CreateDirectory(directory);

            var extension = SelectedFragment.Kind == FragmentKind.RichText ? ".html" : ".txt";
            var path = Path.Combine(directory, $"{SelectedFragment.Id:N}{extension}");
            await File.WriteAllTextAsync(path, PreviewText);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            StatusText = $"Opened in editor: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open in editor: {ex.Message}";
        }
    }

    private bool CanOpenInTextEditor() => IsTextEditorAvailable;

    partial void OnIsTextEditorAvailableChanged(bool value) =>
        OpenInTextEditorCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanOpenInImageViewer))]
    private async Task OpenInImageViewerAsync()
    {
        if (SelectedFragment is null)
        {
            return;
        }

        try
        {
            var path = await _previewService.ExportImageToTempFileAsync(SelectedFragment);
            if (path is null)
            {
                StatusText = "Failed to export image preview";
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            StatusText = $"Opened image: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open image: {ex.Message}";
        }
    }

    private bool CanOpenInImageViewer() => HasPreviewImage;

    partial void OnHasPreviewImageChanged(bool value) =>
        OpenInImageViewerCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void OpenFilePreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            StatusText = $"Файл не найден: {path}";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            StatusText = $"Открыто: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось открыть файл: {ex.Message}";
        }
    }

    private async Task LoadFragmentPreviewAsync(ClipboardFragment? fragment)
    {
        var loadVersion = Interlocked.Increment(ref _previewLoadVersion);
        _previewLoadCts?.Cancel();
        _previewLoadCts?.Dispose();
        _previewLoadCts = new CancellationTokenSource();
        var token = _previewLoadCts.Token;

        if (fragment is null)
        {
            PreviewImage = null;
            HasPreviewImage = false;
            PreviewText = string.Empty;
            PreviewFiles = [];
            HasPreviewFiles = false;
            SelectedFragmentSizeText = string.Empty;
            SelectedFragmentFormatsText = string.Empty;
            SelectedFragmentKindText = string.Empty;
            IsTextEditorAvailable = false;
            return;
        }

        SelectedFragmentKindText = FragmentKindDisplayConverter.Format(fragment.Kind);
        SelectedFragmentSizeText = ByteSizeConverter.FormatBytes(fragment.Payloads.Sum(p => p.SizeBytes));
        SelectedFragmentFormatsText = string.Join(", ", fragment.Payloads.Select(p => p.FormatName));
        PreviewText = fragment.PreviewText ?? string.Empty;
        PreviewImage = null;
        HasPreviewImage = false;
        PreviewFiles = [];
        HasPreviewFiles = false;
        IsTextEditorAvailable = false;

        try
        {
            var state = await _previewService.LoadAsync(fragment, token).ConfigureAwait(true);
            if (loadVersion != _previewLoadVersion)
            {
                return;
            }

            PreviewText = state.PreviewText;
            PreviewImage = state.PreviewImage;
            HasPreviewImage = state.PreviewImage is not null;
            PreviewFiles = state.PreviewFiles ?? [];
            HasPreviewFiles = PreviewFiles.Count > 0;
            IsTextEditorAvailable = state.CanOpenInEditor;
        }
        catch (OperationCanceledException)
        {
            // Selection changed.
        }
        catch (ObjectDisposedException)
        {
            // Previous load cancelled and disposed its token source.
        }
    }

    public async Task ExecuteFragmentActionByIdAsync(string pluginId, ClipboardFragment? fragment = null)
    {
        fragment ??= SelectedFragment;
        var menuItem = _fragmentActionMenuItems.FirstOrDefault(item => item.PluginId == pluginId);
        if (menuItem is null || fragment is null)
        {
            return;
        }

        await menuItem.UpdateCanExecuteAsync(fragment);
        if (menuItem.ExecuteCommand.CanExecute(fragment))
        {
            menuItem.ExecuteCommand.Execute(fragment);
        }
    }

    public async Task RefreshStreamContextMenuAsync(ClipStreamEntity? stream) =>
        await UpdateStreamActionMenuItemsAsync(stream ?? SelectedStream);

    public async Task RefreshFragmentContextMenuAsync(ClipboardFragment? fragment) =>
        await UpdateFragmentActionMenuItemsAsync(fragment ?? SelectedFragment);

    private readonly List<FragmentActionMenuItemViewModel> _fragmentActionMenuItems = [];
    private readonly List<StreamActionMenuItemViewModel> _streamActionMenuItems = [];
    private readonly EditStreamContextMenuItemViewModel _editStreamMenuItem;
    private readonly DeleteFragmentContextMenuItemViewModel _deleteFragmentMenuItem;

    private void BuildActionMenuItems()
    {
        _fragmentActionMenuItems.Clear();
        _fragmentActionMenuItems.AddRange(
            _pluginLoader.FragmentActionPlugins
                .OrderBy(plugin => plugin.MenuGroup)
                .ThenBy(plugin => plugin.MenuOrder)
                .Select(plugin => new FragmentActionMenuItemViewModel(plugin, CreateFragmentContext, _statusReporter)));

        FragmentContextMenuItems = new ObservableCollection<IContextMenuItemViewModel>(
        [
            .._fragmentActionMenuItems,
            _deleteFragmentMenuItem
        ]);

        _streamActionMenuItems.Clear();
        _streamActionMenuItems.AddRange(
            _pluginLoader.StreamActionPlugins
                .OrderBy(plugin => plugin.MenuGroup)
                .ThenBy(plugin => plugin.MenuOrder)
                .Select(plugin => new StreamActionMenuItemViewModel(plugin, CreateStreamContext, _statusReporter)));

        StreamContextMenuItems = new ObservableCollection<IContextMenuItemViewModel>(
        [
            _editStreamMenuItem,
            .._streamActionMenuItems
        ]);
    }

    private FragmentActionContext? CreateFragmentContext(ClipboardFragment? fragment)
    {
        fragment ??= SelectedFragment;
        return fragment is null
            ? null
            : _actionContextFactory.CreateFragmentContext(fragment, SelectedStream);
    }

    private StreamActionContext? CreateStreamContext(ClipStreamEntity? stream)
    {
        stream ??= SelectedStream;
        return stream is null
            ? null
            : _actionContextFactory.CreateStreamContext(stream);
    }

    private async Task UpdateFragmentActionMenuItemsAsync(ClipboardFragment? fragment)
    {
        DeleteFragmentCommand.NotifyCanExecuteChanged();
        _deleteFragmentMenuItem.Refresh();
        foreach (var menuItem in _fragmentActionMenuItems)
        {
            await menuItem.UpdateCanExecuteAsync(fragment);
        }
    }

    private async Task UpdateStreamActionMenuItemsAsync(ClipStreamEntity? stream)
    {
        EditStreamCommand.NotifyCanExecuteChanged();
        _editStreamMenuItem.Refresh();
        foreach (var menuItem in _streamActionMenuItems)
        {
            await menuItem.UpdateCanExecuteAsync(stream);
        }
    }

    private void OnFragmentAdded(object? sender, FragmentAddedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (SelectedStream?.Id == e.StreamId)
            {
                Fragments.Insert(0, e.Fragment);
                StatusText = $"New fragment in {SelectedStream.Name}";
            }
        });
    }

    public async Task SaveFragmentTitleAsync(ClipboardFragment fragment, string newTitle)
    {
        newTitle = newTitle.Trim();
        if (string.IsNullOrWhiteSpace(newTitle) || newTitle == fragment.Title)
        {
            return;
        }

        await _fragmentRepository.UpdateTitleAsync(fragment.Id, newTitle);
        fragment.Title = newTitle;
        StatusText = $"Заголовок изменён на \"{newTitle}\"";
    }
}
