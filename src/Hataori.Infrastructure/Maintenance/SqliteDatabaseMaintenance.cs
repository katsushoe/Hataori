using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Maintenance;

/// <summary>SQLiteの期限切れ判定、Retention purge、VACUUMを実行します。</summary>
public sealed class SqliteDatabaseMaintenance(string connectionString, TimeProvider timeProvider)
{
    public async Task<DatabaseMaintenanceResult> ExecuteAsync(DatabaseMaintenanceSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var now = timeProvider.GetUtcNow();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var expiredTasks = await ExpireTasksAsync(connection, now - settings.StaleTaskAge, now, cancellationToken).ConfigureAwait(false);
        var purgedTasks = await PurgeTasksAsync(connection, now - settings.TaskRetention, cancellationToken).ConfigureAwait(false);
        var purgedRuns = await ExecuteDeleteAsync(connection, "DELETE FROM agent_runs WHERE status IN ('completed', 'failed', 'cancelled') AND ended_at_utc < $cutoff;", now - settings.AgentRunRetention, cancellationToken).ConfigureAwait(false);
        var messageCutoff = now - settings.MessageRetention;
        await ExecuteDeleteAsync(connection, "DELETE FROM message_queue WHERE message_id IN (SELECT message_id FROM message_processing WHERE status IN ('responded', 'failed', 'cancelled') AND completed_at_utc < $cutoff);", messageCutoff, cancellationToken).ConfigureAwait(false);
        var purgedMessages = await ExecuteDeleteAsync(connection, "DELETE FROM message_processing WHERE status IN ('responded', 'failed', 'cancelled') AND completed_at_utc < $cutoff;", messageCutoff, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (settings.Vacuum)
        {
            await using var vacuum = connection.CreateCommand();
            vacuum.CommandText = "VACUUM;";
            await vacuum.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return new DatabaseMaintenanceResult(expiredTasks, purgedTasks, purgedRuns, purgedMessages, settings.Vacuum);
    }

    private static async Task<int> ExpireTasksAsync(SqliteConnection connection, DateTimeOffset cutoff, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tasks SET status = 'expired', completed_at_utc = $now, last_activity_at_utc = $now, result = 'Expired by database maintenance.'
            WHERE status = 'active' AND last_activity_at_utc < $cutoff;
            """;
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$cutoff", Format(cutoff));
        var count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (count > 0)
        {
            await using var history = connection.CreateCommand();
            history.CommandText = """
                INSERT INTO task_history (task_id, created_at_utc, event_type, message, progress_percent)
                SELECT task_id, $now, 'expired', 'Expired by database maintenance.', progress_percent
                FROM tasks WHERE status = 'expired' AND completed_at_utc = $now;
                """;
            history.Parameters.AddWithValue("$now", Format(now));
            await history.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return count;
    }

    private static async Task<int> PurgeTasksAsync(SqliteConnection connection, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        const string target = "status <> 'active' AND completed_at_utc < $cutoff";
        await ExecuteDeleteAsync(connection, $"DELETE FROM task_relations WHERE task_id IN (SELECT task_id FROM tasks WHERE {target}) OR related_task_id IN (SELECT task_id FROM tasks WHERE {target});", cutoff, cancellationToken).ConfigureAwait(false);
        await ExecuteDeleteAsync(connection, $"DELETE FROM task_history WHERE task_id IN (SELECT task_id FROM tasks WHERE {target});", cutoff, cancellationToken).ConfigureAwait(false);
        return await ExecuteDeleteAsync(connection, $"DELETE FROM tasks WHERE {target};", cutoff, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> ExecuteDeleteAsync(SqliteConnection connection, string sql, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$cutoff", Format(cutoff));
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
