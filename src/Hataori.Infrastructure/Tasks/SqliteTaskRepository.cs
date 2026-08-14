using System.Globalization;
using Hataori.Application.Tasks;
using Hataori.Core.Tasks;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tasks;

/// <summary>
/// SQLiteを正本とするTask Repositoryです。
/// </summary>
public sealed class SqliteTaskRepository : ITaskRepository
{
    private readonly string _connectionString;

    public SqliteTaskRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS tasks (
                task_id TEXT PRIMARY KEY,
                task_name TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                conversation_id TEXT NULL,
                origin_message_id TEXT NULL,
                status TEXT NOT NULL,
                summary TEXT NOT NULL,
                current_work TEXT NOT NULL,
                progress_percent INTEGER NOT NULL CHECK(progress_percent BETWEEN 0 AND 100),
                started_at_utc TEXT NOT NULL,
                last_activity_at_utc TEXT NOT NULL,
                completed_at_utc TEXT NULL,
                result TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS task_history (
                history_id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                event_type TEXT NOT NULL,
                message TEXT NOT NULL,
                progress_percent INTEGER NOT NULL,
                FOREIGN KEY(task_id) REFERENCES tasks(task_id)
            );
            """;
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(HataoriTask task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertTaskAsync(connection, transaction, task, cancellationToken).ConfigureAwait(false);
        await InsertHistoryAsync(connection, transaction, task, "started", cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<HataoriTask?> GetAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        const string sql = "SELECT task_id, task_name, agent_id, conversation_id, origin_message_id, status, summary, current_work, progress_percent, started_at_utc, last_activity_at_utc, completed_at_utc, result FROM tasks WHERE task_id = $task_id;";
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$task_id", taskId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return HataoriTask.Restore(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), GetNullableString(reader, 3), GetNullableString(reader, 4),
            Enum.Parse<HataoriTaskStatus>(reader.GetString(5), true), reader.GetString(6), reader.GetString(7), reader.GetInt32(8),
            ParseDate(reader.GetString(9)), ParseDate(reader.GetString(10)), reader.IsDBNull(11) ? null : ParseDate(reader.GetString(11)), GetNullableString(reader, 12));
    }

    public async Task UpdateAsync(HataoriTask task, string eventType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await UpsertTaskAsync(connection, transaction, task, cancellationToken).ConfigureAwait(false);
        await InsertHistoryAsync(connection, transaction, task, eventType, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertTaskAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, HataoriTask task, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO tasks (task_id, task_name, agent_id, conversation_id, origin_message_id, status, summary, current_work, progress_percent, started_at_utc, last_activity_at_utc, completed_at_utc, result)
            VALUES ($task_id, $task_name, $agent_id, $conversation_id, $origin_message_id, $status, $summary, $current_work, $progress_percent, $started_at_utc, $last_activity_at_utc, $completed_at_utc, $result)
            ON CONFLICT(task_id) DO UPDATE SET status=$status, summary=$summary, current_work=$current_work, progress_percent=$progress_percent, last_activity_at_utc=$last_activity_at_utc, completed_at_utc=$completed_at_utc, result=$result;
            """;
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        AddParameters(command, task);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertHistoryAsync(SqliteConnection connection, System.Data.Common.DbTransaction transaction, HataoriTask task, string eventType, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO task_history (task_id, created_at_utc, event_type, message, progress_percent) VALUES ($task_id, $created_at_utc, $event_type, $message, $progress_percent);";
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$task_id", task.TaskId);
        command.Parameters.AddWithValue("$created_at_utc", FormatDate(task.LastActivityAtUtc));
        command.Parameters.AddWithValue("$event_type", eventType);
        command.Parameters.AddWithValue("$message", task.CurrentWork);
        command.Parameters.AddWithValue("$progress_percent", task.ProgressPercent);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameters(SqliteCommand command, HataoriTask task)
    {
        command.Parameters.AddWithValue("$task_id", task.TaskId);
        command.Parameters.AddWithValue("$task_name", task.TaskName);
        command.Parameters.AddWithValue("$agent_id", task.AgentId);
        command.Parameters.AddWithValue("$conversation_id", (object?)task.ConversationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$origin_message_id", (object?)task.OriginMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", task.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$summary", task.Summary);
        command.Parameters.AddWithValue("$current_work", task.CurrentWork);
        command.Parameters.AddWithValue("$progress_percent", task.ProgressPercent);
        command.Parameters.AddWithValue("$started_at_utc", FormatDate(task.StartedAtUtc));
        command.Parameters.AddWithValue("$last_activity_at_utc", FormatDate(task.LastActivityAtUtc));
        command.Parameters.AddWithValue("$completed_at_utc", task.CompletedAtUtc is null ? DBNull.Value : FormatDate(task.CompletedAtUtc.Value));
        command.Parameters.AddWithValue("$result", (object?)task.Result ?? DBNull.Value);
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
