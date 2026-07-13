using System.Text.Json;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace ClipStream.Infrastructure.Persistence;

public sealed class SqliteStreamRepository : IStreamRepository
{
    private static readonly Guid DefaultStreamId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly ClipStreamDatabase _database;

    public SqliteStreamRepository(ClipStreamDatabase database) => _database = database;

    public async Task<IReadOnlyList<ClipStreamEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, icon, sort_order, is_pinned FROM streams ORDER BY sort_order, name";
        var results = new List<ClipStreamEntity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadStream(reader));
        }

        return results;
    }

    public async Task<ClipStreamEntity?> GetByIdAsync(Guid streamId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, icon, sort_order, is_pinned FROM streams WHERE id = $id";
        command.Parameters.AddWithValue("$id", streamId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadStream(reader) : null;
    }

    public async Task<ClipStreamEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, icon, sort_order, is_pinned FROM streams WHERE name = $name";
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadStream(reader) : null;
    }

    public async Task SaveAsync(ClipStreamEntity stream, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO streams (id, name, icon, sort_order, is_pinned)
            VALUES ($id, $name, $icon, $sortOrder, $isPinned)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                icon = excluded.icon,
                sort_order = excluded.sort_order,
                is_pinned = excluded.is_pinned
            """;
        command.Parameters.AddWithValue("$id", stream.Id.ToString());
        command.Parameters.AddWithValue("$name", stream.Name);
        command.Parameters.AddWithValue("$icon", (object?)stream.Icon ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", stream.SortOrder);
        command.Parameters.AddWithValue("$isPinned", stream.IsPinned ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnsureDefaultStreamAsync(CancellationToken cancellationToken = default)
    {
        var existing = await GetByIdAsync(DefaultStreamId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await SaveAsync(new ClipStreamEntity(DefaultStreamId, "inbox", "inbox", 0, true), cancellationToken);
    }

    public static Guid GetDefaultStreamId() => DefaultStreamId;

    private static ClipStreamEntity ReadStream(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetInt32(3),
            reader.GetInt32(4) == 1);
}
