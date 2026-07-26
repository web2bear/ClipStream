using ClipStream.Core.Models;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Infrastructure.Storage;

namespace ClipStream.Infrastructure.Tests;

public class SqliteFragmentRepositoryTests
{
    [Fact]
    public async Task MoveToStreamAsync_MovesFragmentBetweenStreams()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "clipstream-infra-" + Guid.NewGuid());
        var dbPath = Path.Combine(tempDir, "test.db");
        var blobRoot = Path.Combine(tempDir, "blobs");
        Directory.CreateDirectory(tempDir);

        try
        {
            var database = new ClipStreamDatabase(dbPath);
            await database.InitializeAsync();
            var streamRepo = new SqliteStreamRepository(database);
            await streamRepo.EnsureDefaultStreamAsync();

            var targetStream = new ClipStreamEntity(Guid.NewGuid(), "work", "folder", 1, false);
            await streamRepo.SaveAsync(targetStream);

            var blobStore = new FileBlobStore(blobRoot);
            var repo = new SqliteFragmentRepository(database, blobStore);
            var key = await blobStore.StoreAsync("move me"u8.ToArray());

            var fragment = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                FragmentKind.Text,
                "move me",
                "app.exe",
                99,
                [new FormatPayload("UnicodeText", key, 7, null)],
                new Dictionary<string, string>(),
                "sha256:test-move");

            var defaultStreamId = SqliteStreamRepository.GetDefaultStreamId();
            await repo.SaveAsync(fragment, defaultStreamId);

            await repo.MoveToStreamAsync(fragment.Id, targetStream.Id);

            var sourceFragments = await repo.GetByStreamAsync(defaultStreamId);
            var targetFragments = await repo.GetByStreamAsync(targetStream.Id);

            Assert.DoesNotContain(sourceFragments, item => item.Id == fragment.Id);
            Assert.Contains(targetFragments, item => item.Id == fragment.Id);
            Assert.Equal(targetStream.Id, await repo.GetStreamIdForFragmentAsync(fragment.Id));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndGet_RoundTripsFragment()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "clipstream-infra-" + Guid.NewGuid());
        var dbPath = Path.Combine(tempDir, "test.db");
        var blobRoot = Path.Combine(tempDir, "blobs");
        Directory.CreateDirectory(tempDir);

        try
        {
            var database = new ClipStreamDatabase(dbPath);
            await database.InitializeAsync();
            var streamRepo = new SqliteStreamRepository(database);
            await streamRepo.EnsureDefaultStreamAsync();
            var blobStore = new FileBlobStore(blobRoot);
            var repo = new SqliteFragmentRepository(database, blobStore);
            var key = await blobStore.StoreAsync("test data"u8.ToArray());

            var fragment = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                FragmentKind.Text,
                "test data",
                "app.exe",
                99,
                [new FormatPayload("UnicodeText", key, 9, null)],
                new Dictionary<string, string>(),
                "sha256:test-roundtrip");

            await repo.SaveAsync(fragment, SqliteStreamRepository.GetDefaultStreamId());
            var loaded = await repo.GetByIdAsync(fragment.Id);

            Assert.NotNull(loaded);
            Assert.Equal(fragment.PreviewText, loaded.PreviewText);
            Assert.Single(loaded.Payloads);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesOrphanBlob()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "clipstream-infra-" + Guid.NewGuid());
        var dbPath = Path.Combine(tempDir, "test.db");
        var blobRoot = Path.Combine(tempDir, "blobs");
        Directory.CreateDirectory(tempDir);

        try
        {
            var database = new ClipStreamDatabase(dbPath);
            await database.InitializeAsync();
            var streamRepo = new SqliteStreamRepository(database);
            await streamRepo.EnsureDefaultStreamAsync();
            var blobStore = new FileBlobStore(blobRoot);
            var repo = new SqliteFragmentRepository(database, blobStore);
            var key = await blobStore.StoreAsync("orphan me"u8.ToArray());

            var fragment = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                FragmentKind.Text,
                "orphan me",
                "app.exe",
                1,
                [new FormatPayload("UnicodeText", key, 9, null)],
                new Dictionary<string, string>(),
                "sha256:orphan");

            await repo.SaveAsync(fragment, SqliteStreamRepository.GetDefaultStreamId());
            Assert.True(await blobStore.ExistsAsync(key));

            await repo.DeleteAsync(fragment.Id);

            Assert.Null(await repo.GetByIdAsync(fragment.Id));
            Assert.False(await blobStore.ExistsAsync(key));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DeleteAsync_KeepsSharedBlobWhenStillReferenced()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "clipstream-infra-" + Guid.NewGuid());
        var dbPath = Path.Combine(tempDir, "test.db");
        var blobRoot = Path.Combine(tempDir, "blobs");
        Directory.CreateDirectory(tempDir);

        try
        {
            var database = new ClipStreamDatabase(dbPath);
            await database.InitializeAsync();
            var streamRepo = new SqliteStreamRepository(database);
            await streamRepo.EnsureDefaultStreamAsync();
            var blobStore = new FileBlobStore(blobRoot);
            var repo = new SqliteFragmentRepository(database, blobStore);
            var key = await blobStore.StoreAsync("shared payload"u8.ToArray());
            var streamId = SqliteStreamRepository.GetDefaultStreamId();

            var first = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                FragmentKind.Text,
                "shared payload",
                "app.exe",
                1,
                [new FormatPayload("UnicodeText", key, 14, null)],
                new Dictionary<string, string>(),
                "sha256:shared-1");

            var second = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddSeconds(1),
                FragmentKind.Text,
                "shared payload",
                "app.exe",
                1,
                [new FormatPayload("UnicodeText", key, 14, null)],
                new Dictionary<string, string>(),
                "sha256:shared-2");

            await repo.SaveAsync(first, streamId);
            await repo.SaveAsync(second, streamId);

            await repo.DeleteAsync(first.Id);

            Assert.True(await blobStore.ExistsAsync(key));
            Assert.NotNull(await repo.GetByIdAsync(second.Id));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempDir, true);
        }
    }
}
