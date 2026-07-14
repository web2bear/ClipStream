using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Storage;

namespace ClipStream.Plugins.Abstractions;

public sealed class FragmentActionContext
{
    public FragmentActionContext(
        ClipboardFragment fragment,
        ClipStreamEntity? owningStream,
        IPluginHost host,
        IFragmentRepository fragments,
        IStreamRepository streams,
        IBlobStore blobStore)
    {
        Fragment = fragment;
        OwningStream = owningStream;
        Host = host;
        Fragments = fragments;
        Streams = streams;
        BlobStore = blobStore;
    }

    public ClipboardFragment Fragment { get; }

    public ClipStreamEntity? OwningStream { get; }

    public IPluginHost Host { get; }

    public IFragmentRepository Fragments { get; }

    public IStreamRepository Streams { get; }

    public IBlobStore BlobStore { get; }

    public IPluginDialogs Dialogs => Host.Dialogs;

    public void ReportStatus(string message) => Host.ReportStatus(message);
}
