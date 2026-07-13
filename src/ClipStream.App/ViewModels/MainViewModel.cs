using System.Collections.ObjectModel;
using System.Windows;
using ClipStream.App.Services;
using ClipStream.App.Windows;
using ClipStream.Clipboard.Paste;
using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ClipStream.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStreamRepository _streamRepository;
    private readonly IFragmentRepository _fragmentRepository;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IObsidianVaultExporter _exporter;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private ObservableCollection<ClipStreamEntity> _streams = [];

    [ObservableProperty]
    private ObservableCollection<ClipboardFragment> _fragments = [];

    [ObservableProperty]
    private ClipStreamEntity? _selectedStream;

    [ObservableProperty]
    private ClipboardFragment? _selectedFragment;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public MainViewModel(
        IStreamRepository streamRepository,
        IFragmentRepository fragmentRepository,
        IClipboardWriter clipboardWriter,
        IObsidianVaultExporter exporter,
        IThemeService themeService)
    {
        _streamRepository = streamRepository;
        _fragmentRepository = fragmentRepository;
        _clipboardWriter = clipboardWriter;
        _exporter = exporter;
        _themeService = themeService;
        _isDarkTheme = themeService.IsDarkTheme;
        _themeService.ThemeChanged += (_, _) => IsDarkTheme = _themeService.IsDarkTheme;
        _fragmentRepository.FragmentAdded += OnFragmentAdded;
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
        ExportStreamCommand.NotifyCanExecuteChanged();
        EditStreamCommand.NotifyCanExecuteChanged();
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

    partial void OnSelectedFragmentChanged(ClipboardFragment? value)
    {
        PasteFragmentCommand.NotifyCanExecuteChanged();
        ExportFragmentCommand.NotifyCanExecuteChanged();
    }

    private bool CanPasteFragment(ClipboardFragment? fragment) => (fragment ?? SelectedFragment) is not null;

    [RelayCommand(CanExecute = nameof(CanPasteFragment))]
    private async Task PasteFragmentAsync(ClipboardFragment? fragment)
    {
        fragment ??= SelectedFragment;
        if (fragment is null)
        {
            StatusText = "Select a fragment to paste";
            return;
        }

        try
        {
            var fullFragment = await _fragmentRepository.GetByIdAsync(fragment.Id) ?? fragment;
            await _clipboardWriter.PasteFragmentToActiveWindowAsync(fullFragment);
            StatusText = "Pasted to active window";
        }
        catch (Exception ex)
        {
            StatusText = $"Paste failed: {ex.Message}";
        }
    }

    private bool CanExportFragment(ClipboardFragment? fragment) => (fragment ?? SelectedFragment) is not null;

    [RelayCommand(CanExecute = nameof(CanExportFragment))]
    private async Task ExportFragmentAsync(ClipboardFragment? fragment)
    {
        fragment ??= SelectedFragment;
        if (fragment is null)
        {
            return;
        }

        var path = PickExportFolder();
        if (path is null)
        {
            return;
        }

        var options = new ObsidianExportOptions
        {
            TargetDirectory = path,
            Layout = ObsidianLayout.SingleFolder
        };

        var result = await _exporter.ExportFragmentAsync(fragment.Id, options);
        StatusText = $"Exported {result.FilesWritten} file(s) to {path}";
    }

    [RelayCommand(CanExecute = nameof(CanExportStream))]
    private async Task ExportStreamAsync(ClipStreamEntity? stream)
    {
        stream ??= SelectedStream;
        if (stream is null)
        {
            return;
        }

        var path = PickExportFolder();
        if (path is null)
        {
            return;
        }

        var options = new ObsidianExportOptions { TargetDirectory = path };
        var result = await _exporter.ExportStreamAsync(stream.Id, options);
        StatusText = $"Exported stream \"{stream.Name}\": {result.FilesWritten} files, {result.AttachmentsCopied} attachments";
    }

    private bool CanExportStream(ClipStreamEntity? stream) => (stream ?? SelectedStream) is not null;

    [RelayCommand]
    private void ToggleTheme() => _themeService.ToggleTheme();

    [RelayCommand]
    private async Task ExportAllAsync()
    {
        var path = PickExportFolder();
        if (path is null)
        {
            return;
        }

        var options = new ObsidianExportOptions { TargetDirectory = path };
        var result = await _exporter.ExportAllAsync(options);
        StatusText = $"Exported vault: {result.FilesWritten} files";
    }

    private static string? PickExportFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Obsidian vault folder"
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
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
}
