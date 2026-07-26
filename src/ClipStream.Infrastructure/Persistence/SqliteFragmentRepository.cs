using System.Text.Json;
using System.Text.RegularExpressions;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace ClipStream.Infrastructure.Persistence;

public sealed class SqliteFragmentRepository : IFragmentRepository
{
    private readonly ClipStreamDatabase _database;

    public SqliteFragmentRepository(ClipStreamDatabase database) => _database = database;

    public event EventHandler<FragmentAddedEventArgs>? FragmentAdded;

    public async Task SaveAsync(ClipboardFragment fragment, Guid streamId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var fragmentCmd = connection.CreateCommand())
        {
            fragmentCmd.Transaction = (SqliteTransaction)transaction;
            fragmentCmd.CommandText = """
                INSERT INTO fragments (id, captured_at, kind, preview_text, source_process, source_process_id, content_hash, metadata_json, title)
                VALUES ($id, $capturedAt, $kind, $preview, $source, $sourceId, $hash, $metadata, $title)
                """;
            fragmentCmd.Parameters.AddWithValue("$id", fragment.Id.ToString());
            fragmentCmd.Parameters.AddWithValue("$capturedAt", fragment.CapturedAt.ToString("O"));
            fragmentCmd.Parameters.AddWithValue("$kind", (int)fragment.Kind);
            fragmentCmd.Parameters.AddWithValue("$preview", (object?)fragment.PreviewText ?? DBNull.Value);
            fragmentCmd.Parameters.AddWithValue("$source", (object?)fragment.SourceProcessName ?? DBNull.Value);
            fragmentCmd.Parameters.AddWithValue("$sourceId", (object?)fragment.SourceProcessId ?? DBNull.Value);
            fragmentCmd.Parameters.AddWithValue("$hash", (object?)fragment.ContentHash ?? DBNull.Value);
            fragmentCmd.Parameters.AddWithValue("$metadata", JsonSerializer.Serialize(fragment.Metadata));
            fragmentCmd.Parameters.AddWithValue("$title", fragment.Title);
            await fragmentCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var payload in fragment.Payloads)
        {
            await using var payloadCmd = connection.CreateCommand();
            payloadCmd.Transaction = (SqliteTransaction)transaction;
            payloadCmd.CommandText = """
                INSERT INTO format_payloads (id, fragment_id, format_name, storage_key, size_bytes, content_hash)
                VALUES ($id, $fragmentId, $format, $key, $size, $hash)
                """;
            payloadCmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            payloadCmd.Parameters.AddWithValue("$fragmentId", fragment.Id.ToString());
            payloadCmd.Parameters.AddWithValue("$format", payload.FormatName);
            payloadCmd.Parameters.AddWithValue("$key", payload.StorageKey);
            payloadCmd.Parameters.AddWithValue("$size", payload.SizeBytes);
            payloadCmd.Parameters.AddWithValue("$hash", (object?)payload.ContentHash ?? DBNull.Value);
            await payloadCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var linkCmd = connection.CreateCommand())
        {
            linkCmd.Transaction = (SqliteTransaction)transaction;
            linkCmd.CommandText = """
                INSERT INTO fragment_streams (fragment_id, stream_id, routed_at)
                VALUES ($fragmentId, $streamId, $routedAt)
                """;
            linkCmd.Parameters.AddWithValue("$fragmentId", fragment.Id.ToString());
            linkCmd.Parameters.AddWithValue("$streamId", streamId.ToString());
            linkCmd.Parameters.AddWithValue("$routedAt", DateTimeOffset.UtcNow.ToString("O"));
            await linkCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        FragmentAdded?.Invoke(this, new FragmentAddedEventArgs { Fragment = fragment, StreamId = streamId });
    }

    public async Task<ClipboardFragment?> GetByIdAsync(Guid fragmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM fragments WHERE id = $id";
        command.Parameters.AddWithValue("$id", fragmentId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var fragment = ReadFragment(reader);
        var payloads = await LoadPayloadsAsync(connection, fragmentId, cancellationToken);
        return fragment with { Payloads = payloads };
    }

    public Task<IReadOnlyList<ClipboardFragment>> GetByStreamAsync(
        Guid streamId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
        => QueryFragmentsAsync(
            """
            SELECT f.* FROM fragments f
            INNER JOIN fragment_streams fs ON f.id = fs.fragment_id
            WHERE fs.stream_id = $streamId
            ORDER BY f.captured_at DESC
            LIMIT $take OFFSET $skip
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("$streamId", streamId.ToString());
                cmd.Parameters.AddWithValue("$take", take);
                cmd.Parameters.AddWithValue("$skip", skip);
            },
            cancellationToken);

    public Task<IReadOnlyList<ClipboardFragment>> GetAllAsync(
        int skip = 0,
        int take = int.MaxValue,
        CancellationToken cancellationToken = default)
        => QueryFragmentsAsync(
            "SELECT * FROM fragments ORDER BY captured_at DESC LIMIT $take OFFSET $skip",
            cmd =>
            {
                cmd.Parameters.AddWithValue("$take", take);
                cmd.Parameters.AddWithValue("$skip", skip);
            },
            cancellationToken);

    public async Task<IReadOnlyList<ClipboardFragment>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
        => await GetAllAsync(0, count, cancellationToken);

    public async Task<Guid?> GetStreamIdForFragmentAsync(Guid fragmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT stream_id FROM fragment_streams WHERE fragment_id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", fragmentId.ToString());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string s ? Guid.Parse(s) : null;
    }

    public async Task MoveToStreamAsync(Guid fragmentId, Guid targetStreamId, CancellationToken cancellationToken = default)
    {
        var currentStreamId = await GetStreamIdForFragmentAsync(fragmentId, cancellationToken);
        if (currentStreamId is null || currentStreamId == targetStreamId)
        {
            return;
        }

        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = (SqliteTransaction)transaction;
            deleteCmd.CommandText = "DELETE FROM fragment_streams WHERE fragment_id = $fragmentId";
            deleteCmd.Parameters.AddWithValue("$fragmentId", fragmentId.ToString());
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = (SqliteTransaction)transaction;
            insertCmd.CommandText = """
                INSERT INTO fragment_streams (fragment_id, stream_id, routed_at)
                VALUES ($fragmentId, $streamId, $routedAt)
                """;
            insertCmd.Parameters.AddWithValue("$fragmentId", fragmentId.ToString());
            insertCmd.Parameters.AddWithValue("$streamId", targetStreamId.ToString());
            insertCmd.Parameters.AddWithValue("$routedAt", DateTimeOffset.UtcNow.ToString("O"));
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> ExistsByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM fragments WHERE content_hash = $hash LIMIT 1";
        command.Parameters.AddWithValue("$hash", contentHash);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    private async Task<IReadOnlyList<ClipboardFragment>> QueryFragmentsAsync(
        string sql,
        Action<SqliteCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure(command);

        var fragments = new List<ClipboardFragment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fragment = ReadFragment(reader);
            var payloads = await LoadPayloadsAsync(connection, fragment.Id, cancellationToken);
            fragments.Add(fragment with { Payloads = payloads });
        }

        return fragments;
    }

    private static async Task<IReadOnlyList<FormatPayload>> LoadPayloadsAsync(
        SqliteConnection connection,
        Guid fragmentId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT format_name, storage_key, size_bytes, content_hash
            FROM format_payloads WHERE fragment_id = $id
            """;
        command.Parameters.AddWithValue("$id", fragmentId.ToString());
        var payloads = new List<FormatPayload>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            payloads.Add(new FormatPayload(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return payloads;
    }

    private static ClipboardFragment ReadFragment(SqliteDataReader reader)
    {
        var metadataJson = reader.IsDBNull(7) ? "{}" : reader.GetString(7);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson) ?? new Dictionary<string, string>();
        var fragment = new ClipboardFragment(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            (FragmentKind)reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            [],
            metadata,
            reader.IsDBNull(6) ? null : reader.GetString(6));
        fragment.Title = reader.IsDBNull(8)
            ? fragment.Kind switch
            {
                FragmentKind.Text or FragmentKind.RichText
                    when fragment.PreviewText is { Length: > 0 } =>
                    fragment.PreviewText.Length > 128 ? fragment.PreviewText[..128] : fragment.PreviewText,
                FragmentKind.Image => $"Изображение от {fragment.CapturedAt:yyyy-MM-dd HH:mm}",
                FragmentKind.Files => $"Файлы от {fragment.CapturedAt:yyyy-MM-dd HH:mm}",
                FragmentKind.Binary => $"Двоичные данные от {fragment.CapturedAt:yyyy-MM-dd HH:mm}",
                FragmentKind.Composite => $"Составной фрагмент от {fragment.CapturedAt:yyyy-MM-dd HH:mm}",
                _ => $"Фрагмент от {fragment.CapturedAt:yyyy-MM-dd HH:mm}"
            }
            : reader.GetString(8);
        return fragment;
    }

    public async Task UpdateTitleAsync(Guid fragmentId, string title, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE fragments SET title = $title WHERE id = $id";
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$id", fragmentId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid fragmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var payloadsCmd = connection.CreateCommand())
        {
            payloadsCmd.Transaction = (SqliteTransaction)transaction;
            payloadsCmd.CommandText = "DELETE FROM format_payloads WHERE fragment_id = $id";
            payloadsCmd.Parameters.AddWithValue("$id", fragmentId.ToString());
            await payloadsCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var streamsCmd = connection.CreateCommand())
        {
            streamsCmd.Transaction = (SqliteTransaction)transaction;
            streamsCmd.CommandText = "DELETE FROM fragment_streams WHERE fragment_id = $id";
            streamsCmd.Parameters.AddWithValue("$id", fragmentId.ToString());
            await streamsCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var fragmentCmd = connection.CreateCommand())
        {
            fragmentCmd.Transaction = (SqliteTransaction)transaction;
            fragmentCmd.CommandText = "DELETE FROM fragments WHERE id = $id";
            fragmentCmd.Parameters.AddWithValue("$id", fragmentId.ToString());
            await fragmentCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
