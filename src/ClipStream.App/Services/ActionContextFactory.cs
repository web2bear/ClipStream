using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Storage;
using ClipStream.Plugins.Abstractions;

namespace ClipStream.App.Services;

public sealed class ActionContextFactory
{
    private readonly IPluginHost _host;
    private readonly IFragmentRepository _fragments;
    private readonly IStreamRepository _streams;
    private readonly IBlobStore _blobStore;

    public ActionContextFactory(
        IPluginHost host,
        IFragmentRepository fragments,
        IStreamRepository streams,
        IBlobStore blobStore)
    {
        _host = host;
        _fragments = fragments;
        _streams = streams;
        _blobStore = blobStore;
    }

    public FragmentActionContext CreateFragmentContext(ClipboardFragment fragment, ClipStreamEntity? owningStream) =>
        new(fragment, owningStream, _host, _fragments, _streams, _blobStore);

    public StreamActionContext CreateStreamContext(ClipStreamEntity stream) =>
        new(stream, _host, _fragments, _streams, _blobStore);
}
