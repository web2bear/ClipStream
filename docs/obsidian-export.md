# Obsidian Export Format

ClipStream can export clipboard fragments to an Obsidian-compatible vault folder.

## Layout

```
{TargetDirectory}/
├── streams/
│   └── {stream-slug}/
│       ├── _index.md
│       └── YYYY/MM/DD/
│           └── HHmmss-slug.md
├── attachments/
│   └── {hash-prefix}/{hash}.{ext}
└── .clipstream/
    └── export-manifest.json
```

## Frontmatter

Each fragment is a Markdown file with YAML frontmatter:

- `id` — fragment GUID
- `created` — capture timestamp (ISO 8601)
- `tags` — includes `clipstream` and `clipstream/{kind}`
- `source` — source process name
- `stream` — stream name
- `kind` — Text, Image, Files, etc.
- `contentHash` — deduplication hash
- `formats` — clipboard format names
- `attachments` — relative paths to copied blobs
- `clipstream.exportedAt` — export timestamp

## Export scopes

- **Single fragment** — `SingleFolder` layout
- **Stream** — stream folder + all fragments
- **Full vault** — all streams and fragments

Export is a one-way snapshot; ClipStream does not sync changes from Obsidian back to SQLite.
