# Plugin API

ClipStream loads plugins that implement types from `ClipStream.Plugins.Abstractions` (and models from `ClipStream.Core`).

## Loading

| Source | How |
|--------|-----|
| Built-in | Registered via DI (`ClipStream.Plugins.BuiltIn`) |
| User DLL | Any non-abstract `IClipStreamPlugin` type in `%AppData%/ClipStream/plugins/*.dll` |

The loader uses a collectible `AssemblyLoadContext`, creates instances with a parameterless constructor via `Activator.CreateInstance`, and classifies each plugin by the interfaces it implements.

Action plugins (`IFragmentActionPlugin`, `IStreamActionPlugin`) also receive `ActivateAsync` / `DeactivateAsync` with an `IPluginHost`.

**Requirements for user plugins:**

1. Target `net8.0`
2. Reference `ClipStream.Plugins.Abstractions` (and `ClipStream.Core` as needed)
3. Public class with a **parameterless constructor**
4. Implement one or more plugin interfaces below
5. Copy the DLL (and its dependencies) to `%AppData%/ClipStream/plugins/`

## Hierarchy

```text
IClipStreamPlugin
├── IClipboardFormatPlugin      — turn raw clipboard capture into a fragment
├── IFragmentEnricherPlugin     — mutate/enrich a fragment after format plugins
└── IClipStreamLifecyclePlugin
    ├── IFragmentActionPlugin   — context-menu action on a fragment
    └── IStreamActionPlugin     — context-menu action on a stream
```

## `PluginDescriptor`

```csharp
public sealed record PluginDescriptor(
    string Id,       // unique id, e.g. "com.example.json"
    string Name,     // display name
    string Version,  // e.g. "1.0.0"
    int Priority);   // lower runs first (format/enricher ordering)
```

Every plugin exposes `Descriptor`. Built-in priorities: text `10`, HTML `20`, image `30`, files `40`, generic binary `1000`.

---

## Format plugin — `IClipboardFormatPlugin`

Called when the clipboard changes. The pipeline picks the **first** plugin (by `Priority`) whose `CanHandle` returns `true`, then calls `ProcessAsync`.

```csharp
public interface IClipboardFormatPlugin : IClipStreamPlugin
{
    bool CanHandle(RawClipboardCapture capture);

    Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken);
}
```

### Inputs

`RawClipboardCapture` contains:

- `CapturedAt`, `ClipboardSequence`
- `SourceProcessName` / `SourceProcessId` (from clipboard owner when available)
- `Formats` — list of `RawFormatData(FormatName, Data)`

`PluginContext` provides:

- `IBlobStore BlobStore` — store raw bytes, get a content-addressed `storageKey`
- `RecentFragments` — last ~20 fragments for heuristics

### Results

| Type | Meaning |
|------|---------|
| `FragmentProduced(fragment)` | Accept this fragment; format stage stops |
| `Skipped(reason)` | Decline after `CanHandle`; pipeline tries the **next** matching format plugin |
| `Enriched(...)` | Treated like a produced fragment with extra payloads; prefer `FragmentProduced` for format plugins |

After a fragment is produced, **enrichers** run, then content-hash dedup may drop duplicates.

### Example: JSON format plugin

```csharp
using System.Text;
using System.Text.Json;
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace SamplePlugins;

public sealed class JsonFormatPlugin : IClipboardFormatPlugin
{
    public PluginDescriptor Descriptor { get; } =
        new("sample.json", "JSON text", "1.0.0", priority: 15);

    public bool CanHandle(RawClipboardCapture capture)
    {
        var text = TryGetUnicodeText(capture);
        if (text is null)
        {
            return false;
        }

        text = text.Trim();
        return (text.StartsWith('{') || text.StartsWith('['))
            && IsValidJson(text);
    }

    public async Task<PluginProcessResult> ProcessAsync(
        RawClipboardCapture capture,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var format = capture.Formats.First(f =>
            f.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain");

        var text = Encoding.Unicode.GetString(format.Data).TrimEnd('\0');
        var pretty = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonElement>(text),
            new JsonSerializerOptions { WriteIndented = true });

        var bytes = Encoding.Unicode.GetBytes(pretty);
        var key = await context.BlobStore.StoreAsync(bytes, cancellationToken);
        var hash = ContentHashHelper.ComputeCaptureHash(capture.Formats);

        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            capture.CapturedAt,
            FragmentKind.Text,
            pretty.Length > 500 ? pretty[..500] : pretty,
            capture.SourceProcessName,
            capture.SourceProcessId,
            [
                new FormatPayload(
                    format.FormatName,
                    key,
                    bytes.Length,
                    ContentHashHelper.ComputeBlobHash(bytes))
            ],
            new Dictionary<string, string>
            {
                ["sample.kind"] = "json"
            },
            hash);

        fragment.Title = "JSON";
        return new FragmentProduced(fragment);
    }

    private static string? TryGetUnicodeText(RawClipboardCapture capture)
    {
        var format = capture.Formats.FirstOrDefault(f =>
            f.FormatName is "UnicodeText" or "CF_UNICODETEXT" or "text/plain");
        return format is null
            ? null
            : Encoding.Unicode.GetString(format.Data).TrimEnd('\0');
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
```

Tips:

- Store every payload you want to paste later via `context.BlobStore.StoreAsync`.
- Set `ContentHash` with `ContentHashHelper.ComputeCaptureHash` so identical captures are skipped.
- Use `Metadata` for plugin-specific tags (string dictionary).

---

## Enricher — `IFragmentEnricherPlugin`

Runs for every produced fragment, ordered by `Priority`. Return a (possibly new) `ClipboardFragment` record.

```csharp
public interface IFragmentEnricherPlugin : IClipStreamPlugin
{
    bool CanEnrich(ClipboardFragment fragment);

    Task<ClipboardFragment> EnrichAsync(
        ClipboardFragment fragment,
        PluginContext context,
        CancellationToken cancellationToken);
}
```

### Example: tag long text

```csharp
using ClipStream.Core.Models;
using ClipStream.Plugins.Abstractions;

namespace SamplePlugins;

public sealed class LongTextTagEnricher : IFragmentEnricherPlugin
{
    public PluginDescriptor Descriptor { get; } =
        new("sample.enrich.long-text", "Long text tagger", "1.0.0", 100);

    public bool CanEnrich(ClipboardFragment fragment) =>
        fragment.Kind is FragmentKind.Text or FragmentKind.RichText
        && fragment.PreviewText is { Length: > 200 };

    public Task<ClipboardFragment> EnrichAsync(
        ClipboardFragment fragment,
        PluginContext context,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(fragment.Metadata)
        {
            ["sample.long"] = "true"
        };

        return Task.FromResult(fragment with { Metadata = metadata });
    }
}
```

---

## Fragment action — `IFragmentActionPlugin`

Appears in the fragment context menu (and can be the default double-click action when selected by the host). Requires lifecycle methods.

```csharp
public interface IFragmentActionPlugin : IClipStreamLifecyclePlugin
{
    string MenuTitle { get; }
    string? MenuGroup { get; }   // menu section label
    int MenuOrder { get; }       // lower = higher in group

    Task<bool> CanExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default);
    Task ExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default);
}
```

`FragmentActionContext` gives you:

- `Fragment`, `OwningStream`
- `Host` / `Dialogs` / `ReportStatus`
- `Fragments`, `Streams`, `BlobStore` repositories

### Example: copy preview to a file

```csharp
using System.Text;
using ClipStream.Plugins.Abstractions;

namespace SamplePlugins;

public sealed class SavePreviewActionPlugin : IFragmentActionPlugin
{
    public PluginDescriptor Descriptor { get; } =
        new("sample.action.save-preview", "Save preview…", "1.0.0", 50);

    public string MenuTitle => "Save preview to file…";
    public string? MenuGroup => "Export";
    public int MenuOrder => 10;

    public Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CanExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(context.Fragment.PreviewText));

    public async Task ExecuteAsync(FragmentActionContext context, CancellationToken cancellationToken = default)
    {
        var folder = await context.Dialogs.PickFolderAsync(
            "Choose folder for preview.txt",
            cancellationToken);
        if (folder is null)
        {
            return;
        }

        var path = Path.Combine(folder, $"{context.Fragment.Id:N}-preview.txt");
        await File.WriteAllTextAsync(
            path,
            context.Fragment.PreviewText ?? string.Empty,
            Encoding.UTF8,
            cancellationToken);

        context.ReportStatus($"Saved {path}");
    }
}
```

Resolve app services from `host.Services` in `ActivateAsync` when you need paste, export, etc. (see built-in `PasteFragmentActionPlugin`).

---

## Stream action — `IStreamActionPlugin`

Same pattern as fragment actions, but for a stream (stream list context menu).

```csharp
public interface IStreamActionPlugin : IClipStreamLifecyclePlugin
{
    string MenuTitle { get; }
    string? MenuGroup { get; }
    int MenuOrder { get; }

    Task<bool> CanExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default);
    Task ExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default);
}
```

### Example: count fragments in stream

```csharp
using ClipStream.Plugins.Abstractions;

namespace SamplePlugins;

public sealed class CountStreamActionPlugin : IStreamActionPlugin
{
    public PluginDescriptor Descriptor { get; } =
        new("sample.action.count-stream", "Count fragments", "1.0.0", 30);

    public string MenuTitle => "Count fragments";
    public string? MenuGroup => "Info";
    public int MenuOrder => 0;

    public Task ActivateAsync(IPluginHost host, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task DeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> CanExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public async Task ExecuteAsync(StreamActionContext context, CancellationToken cancellationToken = default)
    {
        var items = await context.Fragments.GetByStreamAsync(context.Stream.Id, limit: 10_000, cancellationToken);
        context.ReportStatus($"Stream \"{context.Stream.Name}\": {items.Count} fragment(s)");
    }
}
```

---

## Host and dialogs

```csharp
public interface IPluginHost
{
    IServiceProvider Services { get; }
    IPluginDialogs Dialogs { get; }
    void ReportStatus(string message);
}

public interface IPluginDialogs
{
    Task<string?> PickFolderAsync(string description, CancellationToken cancellationToken = default);
}
```

`ReportStatus` updates the UI status bar.

---

## Minimal project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ClipStream.Plugins.Abstractions\ClipStream.Plugins.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

When shipping without source references, reference the built `ClipStream.Plugins.Abstractions.dll` and `ClipStream.Core.dll` that match the installed app.

---

## Pipeline order (capture)

1. Clipboard change (after privacy filter)
2. Format plugins by ascending `Priority` → first `CanHandle` wins
3. Enrichers by ascending `Priority`
4. Skip save if `ContentHash` already exists
5. Route to a stream and persist

Action plugins are independent: they run only when the user invokes a menu command.
