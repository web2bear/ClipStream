using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Storage;

namespace ClipStream.Plugins.Abstractions;

public sealed class StreamActionContext
{
    public StreamActionContext(
        ClipStreamEntity stream,
        IPluginHost host,
        IFragmentRepository fragments,
        IStreamRepository streams,
        IBlobStore blobStore)
    {
        Stream = stream;
        Host = host;
        Fragments = fragments;
        Streams = streams;
        BlobStore = blobStore;
    }

    public ClipStreamEntity Stream { get; }

    public IPluginHost Host { get; }

    public IFragmentRepository Fragments { get; }

    public IStreamRepository Streams { get; }

    public IBlobStore BlobStore { get; }

    public IPluginDialogs Dialogs => Host.Dialogs;

    public void ReportStatus(string message) => Host.ReportStatus(message);
}
