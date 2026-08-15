using System.Globalization;
using Hataori.Application.Messages;
using Hataori.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Messages;

/// <summary>
/// Itogurumaメッセージの処理状態とFIFOキューをSQLiteへ永続化します。
/// </summary>
public sealed class SqliteMessageQueueRepository(string connectionString) : IMessageQueueRepository
{
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private int _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _initialized) != 0)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized != 0)
            {
                return;
            }

            const string sql = """
            CREATE TABLE IF NOT EXISTS message_processing (
                message_id TEXT PRIMARY KEY,
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                sender_agent_id TEXT NOT NULL,
                reply_to_message_id TEXT NULL,
                message_type TEXT NOT NULL,
                body TEXT NOT NULL,
                payload_json TEXT NULL,
                status TEXT NOT NULL,
                received_at_utc TEXT NOT NULL,
                started_at_utc TEXT NULL,
                completed_at_utc TEXT NULL,
                error TEXT NULL,
                reply_attempt_count INTEGER NOT NULL DEFAULT 0,
                next_reply_attempt_at_utc TEXT NULL,
                reply_error TEXT NULL,
                reply_message_id TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS message_queue (
                queue_id INTEGER PRIMARY KEY AUTOINCREMENT,
                message_id TEXT NOT NULL UNIQUE,
                conversation_id TEXT NOT NULL,
                agent_id TEXT NOT NULL,
                priority INTEGER NOT NULL DEFAULT 0,
                enqueued_at_utc TEXT NOT NULL,
                sequence INTEGER NOT NULL UNIQUE,
                FOREIGN KEY(message_id) REFERENCES message_processing(message_id)
            );
            CREATE INDEX IF NOT EXISTS ix_message_processing_status ON message_processing(status, agent_id);
            CREATE INDEX IF NOT EXISTS ix_message_queue_fifo ON message_queue(priority DESC, sequence ASC);
            """;
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "reply_attempt_count", "INTEGER NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "next_reply_attempt_at_utc", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "reply_error", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "reply_message_id", "TEXT NULL", cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _initialized, 1);
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<bool> EnqueueAsync(IncomingMessage message, int priority, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);

        await using var processing = connection.CreateCommand();
        processing.Transaction = (SqliteTransaction)transaction;
        processing.CommandText = """
            INSERT INTO message_processing
                (message_id, conversation_id, agent_id, sender_agent_id, reply_to_message_id, message_type, body, payload_json, status, received_at_utc)
            VALUES
                ($message_id, $conversation_id, $agent_id, $sender_agent_id, $reply_to_message_id, $message_type, $body, $payload_json, 'received', $received_at_utc)
            ON CONFLICT(message_id) DO NOTHING;
            """;
        AddMessageParameters(processing, message);
        var inserted = await processing.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        if (inserted)
        {
            await using var queue = connection.CreateCommand();
            queue.Transaction = (SqliteTransaction)transaction;
            queue.CommandText = """
                INSERT INTO message_queue (message_id, conversation_id, agent_id, priority, enqueued_at_utc, sequence)
                VALUES ($message_id, $conversation_id, $agent_id, $priority, $enqueued_at_utc,
                    COALESCE((SELECT MAX(sequence) + 1 FROM message_queue), 1));
                UPDATE message_processing SET status = 'queued' WHERE message_id = $message_id;
                """;
            queue.Parameters.AddWithValue("$message_id", message.MessageId);
            queue.Parameters.AddWithValue("$conversation_id", message.ConversationId);
            queue.Parameters.AddWithValue("$agent_id", message.AgentId);
            queue.Parameters.AddWithValue("$priority", priority);
            queue.Parameters.AddWithValue("$enqueued_at_utc", FormatDate(message.ReceivedAtUtc));
            await queue.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted;
    }

    public async Task<IReadOnlyList<QueuedMessage>> ListAsync(string? agentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT q.queue_id, q.sequence, q.priority, p.message_id, p.conversation_id, p.agent_id,
                   p.sender_agent_id, p.reply_to_message_id, p.message_type, p.body, p.payload_json,
                   p.received_at_utc, q.enqueued_at_utc
            FROM message_queue q
            JOIN message_processing p ON p.message_id = q.message_id
            WHERE ($agent_id IS NULL OR q.agent_id = $agent_id)
            ORDER BY q.priority DESC, q.sequence ASC;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$agent_id", string.IsNullOrWhiteSpace(agentId) ? DBNull.Value : agentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var messages = new List<QueuedMessage>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            messages.Add(ReadQueuedMessage(reader));
        }

        return messages;
    }

    public async Task<QueuedMessage?> TryClaimNextAsync(string? agentId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var select = connection.CreateCommand();
        select.Transaction = (SqliteTransaction)transaction;
        select.CommandText = """
            SELECT q.queue_id, q.sequence, q.priority, p.message_id, p.conversation_id, p.agent_id,
                   p.sender_agent_id, p.reply_to_message_id, p.message_type, p.body, p.payload_json,
                   p.received_at_utc, q.enqueued_at_utc
            FROM message_queue q
            JOIN message_processing p ON p.message_id = q.message_id
            WHERE ($agent_id IS NULL OR q.agent_id = $agent_id)
              AND NOT EXISTS (
                  SELECT 1 FROM message_processing active
                  WHERE active.conversation_id = q.conversation_id
                    AND active.agent_id = q.agent_id
                    AND active.message_id <> q.message_id
                    AND active.status IN ('starting', 'running')
              )
            ORDER BY q.priority DESC, q.sequence ASC
            LIMIT 1;
            """;
        select.Parameters.AddWithValue("$agent_id", string.IsNullOrWhiteSpace(agentId) ? DBNull.Value : agentId);
        QueuedMessage? message;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            message = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadQueuedMessage(reader) : null;
        }

        if (message is not null)
        {
            await using var claim = connection.CreateCommand();
            claim.Transaction = (SqliteTransaction)transaction;
            claim.CommandText = """
                DELETE FROM message_queue WHERE queue_id = $queue_id;
                UPDATE message_processing
                SET status = 'starting', started_at_utc = $started_at_utc
                WHERE message_id = $message_id;
                """;
            claim.Parameters.AddWithValue("$queue_id", message.QueueId);
            claim.Parameters.AddWithValue("$message_id", message.Message.MessageId);
            claim.Parameters.AddWithValue("$started_at_utc", FormatDate(DateTimeOffset.UtcNow));
            await claim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return message;
    }

    public Task MarkRunningAsync(string messageId, CancellationToken cancellationToken) =>
        UpdateStatusAsync(messageId, "running", null, null, cancellationToken);

    public async Task MarkRespondedAsync(string messageId, string replyMessageId, DateTimeOffset respondedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replyMessageId);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE message_processing
            SET status = 'responded', completed_at_utc = $responded_at_utc, error = NULL,
                next_reply_attempt_at_utc = NULL, reply_error = NULL, reply_message_id = $reply_message_id
            WHERE message_id = $message_id;
            """;
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$reply_message_id", replyMessageId);
        command.Parameters.AddWithValue("$responded_at_utc", FormatDate(respondedAtUtc));
        await EnsureSingleUpdateAsync(command, messageId, cancellationToken).ConfigureAwait(false);
    }

    public Task MarkFailedAsync(string messageId, string error, DateTimeOffset failedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return UpdateStatusAsync(messageId, "failed", error, failedAtUtc, cancellationToken);
    }

    public async Task<MessageProcessingStatus?> GetProcessingStatusAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM message_processing WHERE message_id = $message_id;";
        command.Parameters.AddWithValue("$message_id", messageId);
        var value = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null ? null : Enum.Parse<MessageProcessingStatus>(value, true);
    }

    public async Task ScheduleReplyRetryAsync(string messageId, string error, int attemptCount, DateTimeOffset failedAtUtc, DateTimeOffset? nextAttemptAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptCount);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE message_processing
            SET status = $status, completed_at_utc = $completed_at_utc, error = $error,
                reply_attempt_count = $attempt_count, next_reply_attempt_at_utc = $next_attempt_at_utc,
                reply_error = $error
            WHERE message_id = $message_id;
            """;
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$attempt_count", attemptCount);
        command.Parameters.AddWithValue("$status", nextAttemptAtUtc is null ? "failed" : "running");
        command.Parameters.AddWithValue("$completed_at_utc", nextAttemptAtUtc is null ? FormatDate(failedAtUtc) : DBNull.Value);
        command.Parameters.AddWithValue("$next_attempt_at_utc", nextAttemptAtUtc is null ? DBNull.Value : FormatDate(nextAttemptAtUtc.Value));
        await EnsureSingleUpdateAsync(command, messageId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PendingReply>> GetDueReplyRetriesAsync(DateTimeOffset dueAtUtc, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        const string sql = """
            WITH pending AS (
                SELECT p.message_id, p.conversation_id, p.sender_agent_id,
                       (SELECT r.final_message FROM agent_runs r
                        WHERE r.message_id = p.message_id AND r.status = 'completed'
                        ORDER BY r.queued_at_utc DESC LIMIT 1) AS final_message,
                       p.reply_attempt_count, p.next_reply_attempt_at_utc
                FROM message_processing p
                WHERE p.status IN ('running', 'failed')
                  AND p.next_reply_attempt_at_utc IS NOT NULL
                  AND p.next_reply_attempt_at_utc <= $due_at_utc
            )
            SELECT message_id, conversation_id, sender_agent_id, final_message, reply_attempt_count, next_reply_attempt_at_utc
            FROM pending
            WHERE final_message IS NOT NULL
            ORDER BY next_reply_attempt_at_utc, message_id
            LIMIT $limit;
            """;
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$due_at_utc", FormatDate(dueAtUtc));
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var replies = new List<PendingReply>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            replies.Add(new PendingReply(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), ParseDate(reader.GetString(5))));
        }

        return replies;
    }

    private async Task UpdateStatusAsync(string messageId, string status, string? error, DateTimeOffset? completedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE message_processing
            SET status = $status, error = $error, completed_at_utc = $completed_at_utc
            WHERE message_id = $message_id;
            """;
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed_at_utc", completedAtUtc is null ? DBNull.Value : FormatDate(completedAtUtc.Value));
        await EnsureSingleUpdateAsync(command, messageId, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSingleUpdateAsync(SqliteCommand command, string messageId, CancellationToken cancellationToken)
    {
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new KeyNotFoundException($"Message processing '{messageId}' was not found.");
        }
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string columnName, string definition, CancellationToken cancellationToken)
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "PRAGMA table_info(message_processing);";
        await using var reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE message_processing ADD COLUMN {columnName} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddMessageParameters(SqliteCommand command, IncomingMessage message)
    {
        command.Parameters.AddWithValue("$message_id", message.MessageId);
        command.Parameters.AddWithValue("$conversation_id", message.ConversationId);
        command.Parameters.AddWithValue("$agent_id", message.AgentId);
        command.Parameters.AddWithValue("$sender_agent_id", message.SenderAgentId);
        command.Parameters.AddWithValue("$reply_to_message_id", (object?)message.ReplyToMessageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$message_type", message.MessageType);
        command.Parameters.AddWithValue("$body", message.Body);
        command.Parameters.AddWithValue("$payload_json", (object?)message.PayloadJson ?? DBNull.Value);
        command.Parameters.AddWithValue("$received_at_utc", FormatDate(message.ReceivedAtUtc));
    }

    private static QueuedMessage ReadQueuedMessage(SqliteDataReader reader)
    {
        var message = new IncomingMessage(
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
            GetNullableString(reader, 7), reader.GetString(8), reader.GetString(9), GetNullableString(reader, 10),
            ParseDate(reader.GetString(11)));
        return new QueuedMessage(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2), message, ParseDate(reader.GetString(12)));
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
