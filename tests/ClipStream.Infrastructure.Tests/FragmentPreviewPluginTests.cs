using System.Text;
using ClipStream.Core.Models;
using ClipStream.Core.Storage;
using ClipStream.Infrastructure.Plugins;
using ClipStream.Plugins.Abstractions;
using ClipStream.Plugins.BuiltIn;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipStream.Infrastructure.Tests;

public class FragmentPreviewPluginTests
{
    [Fact]
    public void TextPreviewPlugin_CanPreview_OnlyTextAndRichText()
    {
        var plugin = new TextPreviewPlugin();

        Assert.True(plugin.CanPreview(CreateFragment(FragmentKind.Text)));
        Assert.True(plugin.CanPreview(CreateFragment(FragmentKind.RichText)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Image)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Files)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Binary)));
    }

    [Fact]
    public async Task TextPreviewPlugin_BuildPreviewAsync_ReturnsTextFromBlob()
    {
        var blobStore = new MemoryBlobStore();
        var bytes = Encoding.Unicode.GetBytes("hello preview");
        var key = await blobStore.StoreAsync(bytes);
        var fragment = CreateFragment(
            FragmentKind.Text,
            previewText: "trunc",
            payloads: [new FormatPayload("UnicodeText", key, bytes.Length, null)]);

        var result = await new TextPreviewPlugin().BuildPreviewAsync(
            fragment,
            new FragmentPreviewContext(blobStore));

        var text = Assert.IsType<TextFragmentPreview>(result);
        Assert.Equal("hello preview", text.Text);
        Assert.True(text.CanOpenInEditor);
    }

    [Fact]
    public void ImagePreviewPlugin_CanPreview_OnlyImage()
    {
        var plugin = new ImagePreviewPlugin();

        Assert.True(plugin.CanPreview(CreateFragment(FragmentKind.Image)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Text)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.RichText)));
    }

    [Fact]
    public async Task ImagePreviewPlugin_BuildPreviewAsync_ReturnsImageBytes()
    {
        var blobStore = new MemoryBlobStore();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var key = await blobStore.StoreAsync(bytes);
        var fragment = CreateFragment(
            FragmentKind.Image,
            previewText: "[Image]",
            payloads: [new FormatPayload("PNG", key, bytes.Length, null)]);

        var result = await new ImagePreviewPlugin().BuildPreviewAsync(
            fragment,
            new FragmentPreviewContext(blobStore));

        var image = Assert.IsType<ImageFragmentPreview>(result);
        Assert.Equal("PNG", image.FormatName);
        Assert.Equal(bytes, image.Data);
    }

    [Fact]
    public void PluginLoader_RegisterBuiltInPlugins_ClassifiesPreviewPlugins()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        loader.RegisterBuiltInPlugins([new TextPreviewPlugin(), new ImagePreviewPlugin()]);

        Assert.Equal(2, loader.PreviewPlugins.Count);
        Assert.Contains(loader.PreviewPlugins, p => p.Descriptor.Id == "builtin.preview.text");
        Assert.Contains(loader.PreviewPlugins, p => p.Descriptor.Id == "builtin.preview.image");
        Assert.Empty(loader.FormatPlugins);
    }

    private static ClipboardFragment CreateFragment(
        FragmentKind kind,
        string? previewText = null,
        IReadOnlyList<FormatPayload>? payloads = null) =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            kind,
            previewText,
            "test.exe",
            1,
            payloads ?? [],
            new Dictionary<string, string>(),
            "sha256:test");

    private sealed class MemoryBlobStore : IBlobStore
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public Task<string> StoreAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var key = Guid.NewGuid().ToString("N");
            _store[key] = data;
            return Task.FromResult(key);
        }

        public Task<byte[]?> GetAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(storageKey, out var data) ? data : null);

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.ContainsKey(storageKey));
    }
}
