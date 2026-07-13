using System.Text.Json;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace ClipStream.Infrastructure.Persistence;

public sealed class SqliteRoutingRuleRepository : IRoutingRuleRepository
{
    private readonly ClipStreamDatabase _database;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteRoutingRuleRepository(ClipStreamDatabase database) => _database = database;

    public async Task<IReadOnlyList<RoutingRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, stream_id, priority, condition_json FROM routing_rules ORDER BY priority";
        var rules = new List<RoutingRule>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rules.Add(ReadRule(reader));
        }

        return rules;
    }

    public async Task SaveAsync(RoutingRule rule, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO routing_rules (id, stream_id, priority, condition_json)
            VALUES ($id, $streamId, $priority, $condition)
            ON CONFLICT(id) DO UPDATE SET
                stream_id = excluded.stream_id,
                priority = excluded.priority,
                condition_json = excluded.condition_json
            """;
        command.Parameters.AddWithValue("$id", rule.Id.ToString());
        command.Parameters.AddWithValue("$streamId", rule.TargetStreamId.ToString());
        command.Parameters.AddWithValue("$priority", rule.Priority);
        command.Parameters.AddWithValue("$condition", SerializeCondition(rule.Condition));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.OpenConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM routing_rules WHERE id = $id";
        command.Parameters.AddWithValue("$id", ruleId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RoutingRule ReadRule(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            DeserializeCondition(reader.GetString(3)));

    private static string SerializeCondition(RoutingCondition condition)
    {
        var dataJson = JsonSerializer.Serialize(condition, condition.GetType(), JsonOptions);
        return $"{{\"type\":\"{condition.GetType().Name}\",\"data\":{dataJson}}}";
    }

    private static RoutingCondition DeserializeCondition(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var type = doc.RootElement.GetProperty("type").GetString();
        var data = doc.RootElement.GetProperty("data").GetRawText();
        return type switch
        {
            nameof(TextMatchesRegexCondition) => JsonSerializer.Deserialize<TextMatchesRegexCondition>(data, JsonOptions)!,
            nameof(KindIsCondition) => JsonSerializer.Deserialize<KindIsCondition>(data, JsonOptions)!,
            nameof(SourceProcessIsCondition) => JsonSerializer.Deserialize<SourceProcessIsCondition>(data, JsonOptions)!,
            nameof(FormatPresentCondition) => JsonSerializer.Deserialize<FormatPresentCondition>(data, JsonOptions)!,
            _ => throw new InvalidOperationException($"Unknown routing condition type: {type}")
        };
    }
}
