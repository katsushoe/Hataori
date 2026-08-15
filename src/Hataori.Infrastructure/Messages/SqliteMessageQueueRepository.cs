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
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
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
                error TEXT NULL
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

    public Task MarkFailedAsync(string messageId, string error, DateTimeOffset failedAtUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return UpdateStatusAsync(messageId, "failed", error, failedAtUtc, cancellationToken);
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
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new KeyNotFoundException($"Message processing '{messageId}' was not found.");
        }
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
