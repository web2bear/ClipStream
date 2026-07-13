using ClipStream.Core.Models;
using ClipStream.Core.Storage;

namespace ClipStream.Plugins.Abstractions;

public sealed record PluginContext(
    IBlobStore BlobStore,
    IReadOnlyList<ClipboardFragment> RecentFragments);

public abstract record PluginProcessResult;

public sealed record FragmentProduced(ClipboardFragment Fragment) : PluginProcessResult;

public sealed record Skipped(string Reason) : PluginProcessResult;

public sealed record Enriched(ClipboardFragment Fragment, IReadOnlyList<FormatPayload> ExtraPayloads) : PluginProcessResult;
