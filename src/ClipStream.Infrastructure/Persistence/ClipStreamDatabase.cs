using Microsoft.Data.Sqlite;

namespace ClipStream.Infrastructure.Persistence;

public sealed class ClipStreamDatabase
{
    private readonly string _connectionString;

    public ClipStreamDatabase(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipStream",
            "clipstream.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path
        }.ToString();
    }

    public string ConnectionString => _connectionString;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = DatabaseSchema.Sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public SqliteConnection OpenConnection() => new(_connectionString);
}
