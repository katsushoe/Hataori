using System.Globalization;
using Hataori.Application.Sessions;
using Hataori.Core.Sessions;
using Hataori.Core.Workspaces;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Sessions;

public sealed class SqliteConversationSessionRepository(string connectionString) : IConversationSessionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS conversation_sessions (
                workspace_id TEXT NOT NULL DEFAULT 'default',
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                native_session_id TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                last_used_at_utc TEXT NOT NULL,
                invalidated_at_utc TEXT NULL,
                PRIMARY KEY(workspace_id, conversation_id, agent_id)
            );
            CREATE INDEX IF NOT EXISTS ix_conversation_sessions_status_agent
                ON conversation_sessions(status, agent_id);
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureWorkspaceColumnAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConversationSession?> GetAsync(string conversationId, string agentId, CancellationToken cancellationToken)
        => await GetAsync(WorkspaceId.Default, conversationId, agentId, cancellationToken).ConfigureAwait(false);

    public async Task<ConversationSession?> GetAsync(string workspaceId, string conversationId, string agentId, CancellationToken cancellationToken)
    {
        workspaceId = WorkspaceId.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        const string sql = """
            SELECT workspace_id, conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc
            FROM conversation_sessions
            WHERE workspace_id = $workspace_id AND conversation_id = $conversation_id AND agent_id = $agent_id;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$workspace_id", workspaceId);
        command.Parameters.AddWithValue("$conversation_id", conversationId);
        command.Parameters.AddWithValue("$agent_id", agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSession(reader) : null;
    }

    public async Task<IReadOnlyList<ConversationSession>> ListAsync(ConversationSessionStatus? status, string? agentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT workspace_id, conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc
            FROM conversation_sessions
            WHERE ($status IS NULL OR status = $status) AND ($agent_id IS NULL OR agent_id = $agent_id)
            ORDER BY last_used_at_utc DESC, conversation_id, agent_id;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$status", status is null ? DBNull.Value : status.Value.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$agent_id", string.IsNullOrWhiteSpace(agentId) ? DBNull.Value : agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var sessions = new List<ConversationSession>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions;
    }

    public async Task SaveAsync(ConversationSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        const string sql = """
            INSERT INTO conversation_sessions
                (workspace_id, conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc)
            VALUES
                ($workspace_id, $conversation_id, $agent_id, $native_session_id, $status, $created_at_utc, $last_used_at_utc, $invalidated_at_utc)
            ON CONFLICT(workspace_id, conversation_id, agent_id) DO UPDATE SET
                native_session_id = excluded.native_session_id,
                status = excluded.status,
                last_used_at_utc = excluded.last_used_at_utc,
                invalidated_at_utc = excluded.invalidated_at_utc;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$workspace_id", session.WorkspaceId);
        command.Parameters.AddWithValue("$conversation_id", session.ConversationId);
        command.Parameters.AddWithValue("$agent_id", session.AgentId);
        command.Parameters.AddWithValue("$native_session_id", session.NativeSessionId);
        command.Parameters.AddWithValue("$status", session.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$created_at_utc", FormatDate(session.CreatedAtUtc));
        command.Parameters.AddWithValue("$last_used_at_utc", FormatDate(session.LastUsedAtUtc));
        command.Parameters.AddWithValue("$invalidated_at_utc", session.InvalidatedAtUtc is null ? DBNull.Value : FormatDate(session.InvalidatedAtUtc.Value));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ConversationSession ReadSession(SqliteDataReader reader) => ConversationSession.Restore(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        Enum.Parse<ConversationSessionStatus>(reader.GetString(4), true),
        ParseDate(reader.GetString(5)), ParseDate(reader.GetString(6)),
        reader.IsDBNull(7) ? null : ParseDate(reader.GetString(7)));

    private static async Task EnsureWorkspaceColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "PRAGMA table_info(conversation_sessions);";
        await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), "workspace_id", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var migrate = connection.CreateCommand();
        migrate.Transaction = (SqliteTransaction)transaction;
        migrate.CommandText = """
            DROP INDEX IF EXISTS ix_conversation_sessions_status_agent;
            ALTER TABLE conversation_sessions RENAME TO conversation_sessions_legacy;
            CREATE TABLE conversation_sessions (
                workspace_id TEXT NOT NULL DEFAULT 'default',
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                native_session_id TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                last_used_at_utc TEXT NOT NULL,
                invalidated_at_utc TEXT NULL,
                PRIMARY KEY(workspace_id, conversation_id, agent_id)
            );
            INSERT INTO conversation_sessions
                (workspace_id, conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc)
            SELECT 'default', conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc
            FROM conversation_sessions_legacy;
            DROP TABLE conversation_sessions_legacy;
            CREATE INDEX ix_conversation_sessions_status_agent
                ON conversation_sessions(status, agent_id);
            """;
        await migrate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
