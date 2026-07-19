# Plugins folder

Place custom plugin DLLs in `%AppData%/ClipStream/plugins/` (this repo folder is only a reminder).

Requirements:

- `net8.0` class library
- Public types implementing `IClipStreamPlugin` with a parameterless constructor
- Dependencies copied next to the DLL as needed

Full API reference and examples: [docs/plugins.md](../docs/plugins.md).
