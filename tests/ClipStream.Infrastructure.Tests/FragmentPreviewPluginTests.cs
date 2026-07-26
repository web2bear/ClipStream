using System.IO;
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
    public void FilesPreviewPlugin_CanPreview_OnlyFiles()
    {
        var plugin = new FilesPreviewPlugin();

        Assert.True(plugin.CanPreview(CreateFragment(FragmentKind.Files)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Text)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.RichText)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Image)));
        Assert.False(plugin.CanPreview(CreateFragment(FragmentKind.Binary)));
    }

    [Fact]
    public async Task FilesPreviewPlugin_BuildPreviewAsync_ParsesPathsFromPreviewText()
    {
        var fragment = CreateFragment(
            FragmentKind.Files,
            previewText: "C:\\temp\\a.txt\r\nC:\\temp\\b.exe\n");

        var result = await new FilesPreviewPlugin().BuildPreviewAsync(
            fragment,
            new FragmentPreviewContext(new MemoryBlobStore()));

        var files = Assert.IsType<FilesFragmentPreview>(result);
        Assert.Equal(["C:\\temp\\a.txt", "C:\\temp\\b.exe"], files.Paths);
    }

    [Fact]
    public async Task FilesPreviewPlugin_BuildPreviewAsync_PrefersBlobOverCorruptPreviewText()
    {
        var blobStore = new MemoryBlobStore();
        var path = @"C:\Videos\clip.mp4";
        var drop = BuildUnicodeDropFiles(path);
        var key = await blobStore.StoreAsync(drop);
        var fragment = CreateFragment(
            FragmentKind.Files,
            previewText: "C\0:\0\\\0", // legacy corrupt PreviewText from broken UTF-16 parse
            payloads: [new FormatPayload("CF_HDROP", key, drop.Length, null)]);

        var result = await new FilesPreviewPlugin().BuildPreviewAsync(
            fragment,
            new FragmentPreviewContext(blobStore));

        var files = Assert.IsType<FilesFragmentPreview>(result);
        Assert.Equal([path], files.Paths);
    }

    [Fact]
    public async Task FilesPreviewPlugin_BuildPreviewAsync_ReturnsNullForEmptyPlaceholder()
    {
        var fragment = CreateFragment(FragmentKind.Files, previewText: "[Files]");

        var result = await new FilesPreviewPlugin().BuildPreviewAsync(
            fragment,
            new FragmentPreviewContext(new MemoryBlobStore()));

        Assert.Null(result);
    }

    [Fact]
    public void PluginLoader_RegisterBuiltInPlugins_ClassifiesPreviewPlugins()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        loader.RegisterBuiltInPlugins(
            [new TextPreviewPlugin(), new ImagePreviewPlugin(), new FilesPreviewPlugin()]);

        Assert.Equal(3, loader.PreviewPlugins.Count);
        Assert.Contains(loader.PreviewPlugins, p => p.Descriptor.Id == "builtin.preview.text");
        Assert.Contains(loader.PreviewPlugins, p => p.Descriptor.Id == "builtin.preview.image");
        Assert.Contains(loader.PreviewPlugins, p => p.Descriptor.Id == "builtin.preview.files");
        Assert.Empty(loader.FormatPlugins);
    }

    private static byte[] BuildUnicodeDropFiles(params string[] paths)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(20);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1);

        foreach (var path in paths)
        {
            writer.Write(Encoding.Unicode.GetBytes(path));
            writer.Write((ushort)0);
        }

        writer.Write((ushort)0);
        return stream.ToArray();
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
