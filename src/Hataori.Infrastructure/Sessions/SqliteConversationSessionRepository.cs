using System.Globalization;
using Hataori.Application.Sessions;
using Hataori.Core.Sessions;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Sessions;

public sealed class SqliteConversationSessionRepository(string connectionString) : IConversationSessionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS conversation_sessions (
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                native_session_id TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                last_used_at_utc TEXT NOT NULL,
                invalidated_at_utc TEXT NULL,
                PRIMARY KEY(conversation_id, agent_id)
            );
            CREATE INDEX IF NOT EXISTS ix_conversation_sessions_status_agent
                ON conversation_sessions(status, agent_id);
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConversationSession?> GetAsync(string conversationId, string agentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        const string sql = """
            SELECT conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc
            FROM conversation_sessions
            WHERE conversation_id = $conversation_id AND agent_id = $agent_id;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$conversation_id", conversationId);
        command.Parameters.AddWithValue("$agent_id", agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadSession(reader) : null;
    }

    public async Task<IReadOnlyList<ConversationSession>> ListAsync(ConversationSessionStatus? status, string? agentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc
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
                (conversation_id, agent_id, native_session_id, status, created_at_utc, last_used_at_utc, invalidated_at_utc)
            VALUES
                ($conversation_id, $agent_id, $native_session_id, $status, $created_at_utc, $last_used_at_utc, $invalidated_at_utc)
            ON CONFLICT(conversation_id, agent_id) DO UPDATE SET
                native_session_id = excluded.native_session_id,
                status = excluded.status,
                last_used_at_utc = excluded.last_used_at_utc,
                invalidated_at_utc = excluded.invalidated_at_utc;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
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
        reader.GetString(0), reader.GetString(1), reader.GetString(2),
        Enum.Parse<ConversationSessionStatus>(reader.GetString(3), true),
        ParseDate(reader.GetString(4)), ParseDate(reader.GetString(5)),
        reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)));

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
