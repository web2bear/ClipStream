# ClipStream
!!! AI agent–generated app; no manual coding involved. !!!

Windows clipboard manager with stream-based history, plugin pipeline, and Obsidian vault export.

## Build

```powershell
dotnet build ClipStream.sln
dotnet test ClipStream.sln
dotnet run --project src/ClipStream.App
```

## Architecture

- `ClipStream.App` — WPF UI, tray icon, clipboard host window
- `ClipStream.Core` — domain models and service contracts
- `ClipStream.Clipboard` — Win32 clipboard listener and paste/DnD
- `ClipStream.Infrastructure` — SQLite, blob store, plugins, routing
- `ClipStream.Export` — Obsidian-compatible vault export
- `ClipStream.Plugins.BuiltIn` — text, HTML, image, files, generic plugins

## Data

- Database: `%AppData%/ClipStream/clipstream.db`
- Blobs: `%AppData%/ClipStream/blobs/`
- User plugins: `%AppData%/ClipStream/plugins/*.dll`

## Export

See [docs/obsidian-export.md](docs/obsidian-export.md).
