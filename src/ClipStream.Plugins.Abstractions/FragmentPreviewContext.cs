using ClipStream.Core.Storage;

namespace ClipStream.Plugins.Abstractions;

public sealed record FragmentPreviewContext(IBlobStore BlobStore);

public abstract record FragmentPreviewResult;

public sealed record TextFragmentPreview(string Text, bool CanOpenInEditor = true) : FragmentPreviewResult;

public sealed record ImageFragmentPreview(byte[] Data, string FormatName) : FragmentPreviewResult;
