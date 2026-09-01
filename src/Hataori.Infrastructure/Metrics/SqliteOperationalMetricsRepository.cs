using Hataori.Application.Metrics;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Metrics;

/// <summary>SQLiteの永続データを読み取り専用で集計するMetrics Repositoryです。</summary>
public sealed class SqliteOperationalMetricsRepository(string connectionString, TimeProvider timeProvider) : IOperationalMetricsRepository
{
    /// <inheritdoc />
    public async Task<OperationalMetrics> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tasks = await ReadTasksAsync(connection, workspaceId, cancellationToken).ConfigureAwait(false);
        var messages = await ReadMessagesAsync(connection, workspaceId, cancellationToken).ConfigureAwait(false);
        var runs = await ReadRunsAsync(connection, workspaceId, cancellationToken).ConfigureAwait(false);
        var agents = await ReadAgentsAsync(connection, workspaceId, cancellationToken).ConfigureAwait(false);
        return new OperationalMetrics(workspaceId, timeProvider.GetUtcNow(), tasks, messages, runs, agents);
    }

    private static async Task<TaskMetrics> ReadTasksAsync(SqliteConnection connection, string workspaceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*), SUM(status='active'), SUM(status='completed'), SUM(status='failed'), SUM(status='cancelled'), SUM(status='expired'),
                   COALESCE(100.0 * SUM(status='completed') / NULLIF(SUM(status IN ('completed','failed','cancelled','expired')),0),0),
                   COALESCE(AVG(CASE WHEN completed_at_utc IS NOT NULL THEN (julianday(completed_at_utc)-julianday(started_at_utc))*86400.0 END),0)
            FROM tasks WHERE workspace_id=$workspace_id;
            """;
        await using var reader = await ExecuteReaderAsync(connection, sql, workspaceId, cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new TaskMetrics(Int(reader, 0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Int(reader, 4), Int(reader, 5), Double(reader, 6), Double(reader, 7));
    }

    private static async Task<MessageMetrics> ReadMessagesAsync(SqliteConnection connection, string workspaceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*), SUM(status='queued'), SUM(status='starting'), SUM(status='running'), SUM(status='responded'), SUM(status='failed'), SUM(status='cancelled'),
                   SUM(next_reply_attempt_at_utc IS NOT NULL), COALESCE(SUM(reply_attempt_count),0),
                   COALESCE(100.0 * SUM(status='responded') / NULLIF(SUM(status IN ('responded','failed','cancelled')),0),0),
                   COALESCE(AVG(CASE WHEN completed_at_utc IS NOT NULL THEN (julianday(completed_at_utc)-julianday(received_at_utc))*86400.0 END),0)
            FROM message_processing WHERE workspace_id=$workspace_id;
            """;
        await using var reader = await ExecuteReaderAsync(connection, sql, workspaceId, cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new MessageMetrics(Int(reader, 0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Int(reader, 4), Int(reader, 5), Int(reader, 6), Int(reader, 7), Int(reader, 8), Double(reader, 9), Double(reader, 10));
    }

    private static async Task<AgentRunMetrics> ReadRunsAsync(SqliteConnection connection, string workspaceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*), SUM(status='queued'), SUM(status='starting'), SUM(status='running'), SUM(status='completed'), SUM(status='failed'), SUM(status='cancelled'),
                   COALESCE(100.0 * SUM(status='completed') / NULLIF(SUM(status IN ('completed','failed','cancelled')),0),0),
                   COALESCE(AVG(CASE WHEN started_at_utc IS NOT NULL THEN (julianday(started_at_utc)-julianday(queued_at_utc))*86400.0 END),0),
                   COALESCE(AVG(CASE WHEN ended_at_utc IS NOT NULL AND started_at_utc IS NOT NULL THEN (julianday(ended_at_utc)-julianday(started_at_utc))*86400.0 END),0)
            FROM agent_runs WHERE workspace_id=$workspace_id;
            """;
        await using var reader = await ExecuteReaderAsync(connection, sql, workspaceId, cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new AgentRunMetrics(Int(reader, 0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Int(reader, 4), Int(reader, 5), Int(reader, 6), Double(reader, 7), Double(reader, 8), Double(reader, 9));
    }

    private static async Task<IReadOnlyList<AgentMetrics>> ReadAgentsAsync(SqliteConnection connection, string workspaceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT agent_id, COUNT(*), SUM(status='completed'), SUM(status='failed'), SUM(status='cancelled'),
                   COALESCE(100.0 * SUM(status='completed') / NULLIF(SUM(status IN ('completed','failed','cancelled')),0),0),
                   COALESCE(AVG(CASE WHEN ended_at_utc IS NOT NULL AND started_at_utc IS NOT NULL THEN (julianday(ended_at_utc)-julianday(started_at_utc))*86400.0 END),0)
            FROM agent_runs WHERE workspace_id=$workspace_id GROUP BY agent_id ORDER BY agent_id;
            """;
        await using var reader = await ExecuteReaderAsync(connection, sql, workspaceId, cancellationToken).ConfigureAwait(false);
        var result = new List<AgentMetrics>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new AgentMetrics(reader.GetString(0), Int(reader, 1), Int(reader, 2), Int(reader, 3), Int(reader, 4), Double(reader, 5), Double(reader, 6)));
        }
        return result;
    }

    private static async Task<SqliteDataReader> ExecuteReaderAsync(SqliteConnection connection, string sql, string workspaceId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$workspace_id", workspaceId);
        return await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int Int(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
    private static double Double(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Math.Round(Convert.ToDouble(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture), 3);
}
