using System.Globalization;
using Hataori.Application.Runs;
using Hataori.Core.Runs;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Runs;

public sealed class SqliteAgentRunRepository(string connectionString) : IAgentRunRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS agent_runs (
                run_id TEXT PRIMARY KEY,
                message_id TEXT NOT NULL,
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                native_session_id TEXT NULL,
                process_id INTEGER NULL,
                status TEXT NOT NULL,
                queued_at_utc TEXT NOT NULL,
                started_at_utc TEXT NULL,
                ended_at_utc TEXT NULL,
                exit_code INTEGER NULL,
                final_message TEXT NULL,
                error TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_agent_runs_status_agent ON agent_runs(status, agent_id);
            CREATE INDEX IF NOT EXISTS ix_agent_runs_conversation ON agent_runs(conversation_id, agent_id, queued_at_utc);
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(AgentRun run, CancellationToken cancellationToken) => WriteAsync(run, false, cancellationToken);
    public Task SaveAsync(AgentRun run, CancellationToken cancellationToken) => WriteAsync(run, true, cancellationToken);

    public async Task<AgentRun?> GetAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE run_id = $run_id;";
        command.Parameters.AddWithValue("$run_id", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRun(reader) : null;
    }

    public async Task<IReadOnlyList<AgentRun>> ListAsync(AgentRunStatus? status, string? agentId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE ($status IS NULL OR status = $status) AND ($agent_id IS NULL OR agent_id = $agent_id) ORDER BY queued_at_utc DESC, run_id;";
        command.Parameters.AddWithValue("$status", status is null ? DBNull.Value : status.Value.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$agent_id", string.IsNullOrWhiteSpace(agentId) ? DBNull.Value : agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var runs = new List<AgentRun>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            runs.Add(ReadRun(reader));
        }

        return runs;
    }

    private async Task WriteAsync(AgentRun run, bool update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        const string insert = """
            INSERT INTO agent_runs
                (run_id, message_id, conversation_id, agent_id, native_session_id, process_id, status, queued_at_utc, started_at_utc, ended_at_utc, exit_code, final_message, error)
            VALUES
                ($run_id, $message_id, $conversation_id, $agent_id, $native_session_id, $process_id, $status, $queued_at_utc, $started_at_utc, $ended_at_utc, $exit_code, $final_message, $error);
            """;
        const string upsert = """
            INSERT INTO agent_runs
                (run_id, message_id, conversation_id, agent_id, native_session_id, process_id, status, queued_at_utc, started_at_utc, ended_at_utc, exit_code, final_message, error)
            VALUES
                ($run_id, $message_id, $conversation_id, $agent_id, $native_session_id, $process_id, $status, $queued_at_utc, $started_at_utc, $ended_at_utc, $exit_code, $final_message, $error)
            ON CONFLICT(run_id) DO UPDATE SET
                native_session_id = excluded.native_session_id, process_id = excluded.process_id, status = excluded.status,
                started_at_utc = excluded.started_at_utc, ended_at_utc = excluded.ended_at_utc,
                exit_code = excluded.exit_code, final_message = excluded.final_message, error = excluded.error;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = update ? upsert : insert;
        AddParameters(command, run);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string SelectColumns = "SELECT run_id, message_id, conversation_id, agent_id, native_session_id, process_id, status, queued_at_utc, started_at_utc, ended_at_utc, exit_code, final_message, error FROM agent_runs";

    private static void AddParameters(SqliteCommand command, AgentRun run)
    {
        command.Parameters.AddWithValue("$run_id", run.RunId);
        command.Parameters.AddWithValue("$message_id", run.MessageId);
        command.Parameters.AddWithValue("$conversation_id", run.ConversationId);
        command.Parameters.AddWithValue("$agent_id", run.AgentId);
        command.Parameters.AddWithValue("$native_session_id", (object?)run.NativeSessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$process_id", (object?)run.ProcessId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", run.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$queued_at_utc", FormatDate(run.QueuedAtUtc));
        command.Parameters.AddWithValue("$started_at_utc", run.StartedAtUtc is null ? DBNull.Value : FormatDate(run.StartedAtUtc.Value));
        command.Parameters.AddWithValue("$ended_at_utc", run.EndedAtUtc is null ? DBNull.Value : FormatDate(run.EndedAtUtc.Value));
        command.Parameters.AddWithValue("$exit_code", (object?)run.ExitCode ?? DBNull.Value);
        command.Parameters.AddWithValue("$final_message", (object?)run.FinalMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)run.Error ?? DBNull.Value);
    }

    private static AgentRun ReadRun(SqliteDataReader reader) => AgentRun.Restore(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), GetNullableString(reader, 4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5), Enum.Parse<AgentRunStatus>(reader.GetString(6), true), ParseDate(reader.GetString(7)),
        reader.IsDBNull(8) ? null : ParseDate(reader.GetString(8)), reader.IsDBNull(9) ? null : ParseDate(reader.GetString(9)),
        reader.IsDBNull(10) ? null : reader.GetInt32(10), GetNullableString(reader, 11), GetNullableString(reader, 12));

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
