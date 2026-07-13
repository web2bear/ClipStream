using ClipStream.Core.Export;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Core.Storage;
using ClipStream.Export;
using ClipStream.Infrastructure.Persistence;
using ClipStream.Infrastructure.Routing;
using ClipStream.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipStream.Export.Tests;

public class ObsidianVaultExporterTests
{
    [Fact]
    public async Task ExportFragmentAsync_WritesMarkdownFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "clipstream-export-" + Guid.NewGuid());
        var dbPath = Path.Combine(tempRoot, "db", "test.db");
        var blobRoot = Path.Combine(tempRoot, "blobs");
        var exportDir = Path.Combine(tempRoot, "vault");
        Directory.CreateDirectory(exportDir);

        try
        {
            var database = new ClipStreamDatabase(dbPath);
            await database.InitializeAsync();
            var streamRepo = new SqliteStreamRepository(database);
            await streamRepo.EnsureDefaultStreamAsync();
            var fragmentRepo = new SqliteFragmentRepository(database);
            var blobStore = new FileBlobStore(blobRoot);

            var fragment = new ClipboardFragment(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                FragmentKind.Text,
                "Export test",
                "test.exe",
                1,
                [new FormatPayload("UnicodeText", await blobStore.StoreAsync("Export test"u8.ToArray()), 11, null)],
                new Dictionary<string, string>(),
                "sha256:unique-export-test");

            await fragmentRepo.SaveAsync(fragment, SqliteStreamRepository.GetDefaultStreamId());

            var exporter = new ObsidianVaultExporter(
                fragmentRepo,
                streamRepo,
                new ExportPathBuilder(),
                new MarkdownFragmentWriter(),
                new AttachmentCopier(blobStore),
                NullLogger<ObsidianVaultExporter>.Instance);

            var options = new ObsidianExportOptions
            {
                TargetDirectory = exportDir,
                Layout = ObsidianLayout.SingleFolder
            };

            var result = await exporter.ExportFragmentAsync(fragment.Id, options);

            Assert.Equal(1, result.FilesWritten);
            Assert.Single(result.Items);
            Assert.True(File.Exists(Path.Combine(exportDir, result.Items[0].RelativePath)));
            Assert.True(File.Exists(result.ManifestPath!));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(tempRoot, true);
        }
    }
}
