using System.Globalization;
using Hataori.Application.Agents;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Agents;

/// <summary>SQLite Agent定義Repositoryです。</summary>
public sealed class SqliteAgentDefinitionRepository(string connectionString) : IAgentDefinitionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS agent_definitions (
                workspace_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                enabled INTEGER NOT NULL,
                max_concurrent_runs INTEGER NOT NULL CHECK(max_concurrent_runs BETWEEN 0 AND 64),
                created_at_utc TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY(workspace_id, agent_id)
            );
            CREATE TABLE IF NOT EXISTS agent_definition_history (
                history_id INTEGER PRIMARY KEY AUTOINCREMENT,
                workspace_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                enabled INTEGER NOT NULL,
                max_concurrent_runs INTEGER NOT NULL,
                event_type TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_agent_definition_history_agent
                ON agent_definition_history(workspace_id, agent_id, history_id);
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentDefinition>> ListAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE ($workspace_id IS NULL OR workspace_id = $workspace_id) ORDER BY workspace_id, agent_id;";
        command.Parameters.AddWithValue("$workspace_id", workspaceId is null ? DBNull.Value : workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<AgentDefinition>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) results.Add(ReadDefinition(reader));
        return results;
    }

    public async Task<AgentDefinition?> GetAsync(string workspaceId, string agentId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE workspace_id = $workspace_id AND agent_id = $agent_id;";
        command.Parameters.AddWithValue("$workspace_id", workspaceId);
        command.Parameters.AddWithValue("$agent_id", agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadDefinition(reader) : null;
    }

    public async Task UpsertAsync(AgentDefinition definition, string eventType, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO agent_definitions (workspace_id, agent_id, enabled, max_concurrent_runs, created_at_utc, updated_at_utc)
            VALUES ($workspace_id, $agent_id, $enabled, $max_runs, $created_at, $updated_at)
            ON CONFLICT(workspace_id, agent_id) DO UPDATE SET
                enabled = excluded.enabled, max_concurrent_runs = excluded.max_concurrent_runs, updated_at_utc = excluded.updated_at_utc;
            INSERT INTO agent_definition_history (workspace_id, agent_id, enabled, max_concurrent_runs, event_type, created_at_utc)
            VALUES ($workspace_id, $agent_id, $enabled, $max_runs, $event_type, $updated_at);
            """;
        AddParameters(command, definition);
        command.Parameters.AddWithValue("$event_type", eventType);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentDefinitionHistory>> GetHistoryAsync(string workspaceId, string agentId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT history_id, workspace_id, agent_id, enabled, max_concurrent_runs, event_type, created_at_utc FROM agent_definition_history WHERE workspace_id=$workspace_id AND agent_id=$agent_id ORDER BY history_id;";
        command.Parameters.AddWithValue("$workspace_id", workspaceId);
        command.Parameters.AddWithValue("$agent_id", agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<AgentDefinitionHistory>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new AgentDefinitionHistory(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetInt32(4), reader.GetString(5), ParseDate(reader.GetString(6))));
        }
        return results;
    }

    private const string SelectColumns = "SELECT workspace_id, agent_id, enabled, max_concurrent_runs, created_at_utc, updated_at_utc FROM agent_definitions";
    private static AgentDefinition ReadDefinition(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetInt32(3), ParseDate(reader.GetString(4)), ParseDate(reader.GetString(5)));
    private static void AddParameters(SqliteCommand command, AgentDefinition value)
    {
        command.Parameters.AddWithValue("$workspace_id", value.WorkspaceId);
        command.Parameters.AddWithValue("$agent_id", value.AgentId);
        command.Parameters.AddWithValue("$enabled", value.Enabled);
        command.Parameters.AddWithValue("$max_runs", value.MaxConcurrentRuns);
        command.Parameters.AddWithValue("$created_at", FormatDate(value.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated_at", FormatDate(value.UpdatedAtUtc));
    }
    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
