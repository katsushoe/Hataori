using System.Globalization;
using Hataori.Application.Codex;
using Hataori.Core.Codex;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Codex;

/// <summary>Codex Desktopタスク起動要求を既存Message Queueと同じSQLiteへ保存します。</summary>
public sealed class SqliteCodexTaskLaunchRepository(string connectionString) : ICodexTaskLaunchRepository
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

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS codex_task_launches (
                    message_id TEXT PRIMARY KEY,
                    claim_token TEXT NULL,
                    claimed_at_utc TEXT NULL,
                    lease_until_utc TEXT NULL,
                    status TEXT NOT NULL DEFAULT 'pending',
                    codex_task_id TEXT NULL,
                    started_at_utc TEXT NULL,
                    released_at_utc TEXT NULL,
                    error TEXT NULL,
                    FOREIGN KEY(message_id) REFERENCES message_processing(message_id)
                );
                CREATE INDEX IF NOT EXISTS ix_codex_task_launch_status
                    ON codex_task_launches(status, lease_until_utc);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _initialized, 1);
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<CodexTaskLaunch?> TryClaimAsync(DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var claimToken = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        await using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = """
            SELECT p.message_id, p.working_directory, p.body, p.conversation_id, p.sender_agent_id
            FROM message_queue q
            JOIN message_processing p ON p.message_id = q.message_id
            LEFT JOIN codex_task_launches c ON c.message_id = p.message_id
            WHERE q.agent_id = 'codex'
              AND (c.message_id IS NULL OR c.status = 'pending' OR (c.status = 'claimed' AND c.lease_until_utc <= $now))
            ORDER BY q.priority DESC, q.sequence ASC
            LIMIT 1;
            """;
        select.Parameters.AddWithValue("$now", FormatDate(now));
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        var messageId = reader.GetString(0);
        var workingDirectory = reader.GetString(1);
        var prompt = reader.GetString(2);
        var conversationId = reader.GetString(3);
        var senderAgentId = reader.GetString(4);
        await reader.DisposeAsync().ConfigureAwait(false);

        await using var claim = connection.CreateCommand();
        claim.Transaction = transaction;
        claim.CommandText = """
            INSERT INTO codex_task_launches
                (message_id, claim_token, claimed_at_utc, lease_until_utc, status, codex_task_id, started_at_utc, released_at_utc, error)
            VALUES ($message_id, $claim_token, $claimed_at_utc, $lease_until_utc, 'claimed', NULL, NULL, NULL, NULL)
            ON CONFLICT(message_id) DO UPDATE SET
                claim_token = excluded.claim_token,
                claimed_at_utc = excluded.claimed_at_utc,
                lease_until_utc = excluded.lease_until_utc,
                status = 'claimed',
                error = NULL;
            UPDATE message_processing SET status = 'starting', started_at_utc = $claimed_at_utc WHERE message_id = $message_id;
            """;
        claim.Parameters.AddWithValue("$message_id", messageId);
        claim.Parameters.AddWithValue("$claim_token", claimToken);
        claim.Parameters.AddWithValue("$claimed_at_utc", FormatDate(now));
        claim.Parameters.AddWithValue("$lease_until_utc", FormatDate(leaseUntil));
        await claim.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new CodexTaskLaunch(messageId, claimToken, Path.GetFileName(workingDirectory), workingDirectory, prompt, conversationId, senderAgentId, now, leaseUntil);
    }

    public async Task MarkStartedAsync(string messageId, string claimToken, string codexTaskId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE codex_task_launches
            SET status = 'started', codex_task_id = $codex_task_id, started_at_utc = $started_at_utc, lease_until_utc = NULL
            WHERE message_id = $message_id AND claim_token = $claim_token AND status = 'claimed'
              AND lease_until_utc >= $started_at_utc;
            """;
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$claim_token", claimToken);
        command.Parameters.AddWithValue("$codex_task_id", codexTaskId);
        command.Parameters.AddWithValue("$started_at_utc", FormatDate(startedAtUtc));
        await EnsureUpdatedAsync(command, messageId, cancellationToken).ConfigureAwait(false);

        await using var finalize = connection.CreateCommand();
        finalize.Transaction = transaction;
        finalize.CommandText = """
            DELETE FROM message_queue WHERE message_id = $message_id;
            UPDATE message_processing SET status = 'running' WHERE message_id = $message_id;
            """;
        finalize.Parameters.AddWithValue("$message_id", messageId);
        await finalize.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(string messageId, string claimToken, string error, DateTimeOffset releasedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE codex_task_launches
            SET status = 'pending', claim_token = NULL, lease_until_utc = NULL, released_at_utc = $released_at_utc, error = $error
            WHERE message_id = $message_id AND claim_token = $claim_token AND status = 'claimed';
            """;
        command.Parameters.AddWithValue("$message_id", messageId);
        command.Parameters.AddWithValue("$claim_token", claimToken);
        command.Parameters.AddWithValue("$released_at_utc", FormatDate(releasedAtUtc));
        command.Parameters.AddWithValue("$error", error);
        await EnsureUpdatedAsync(command, messageId, cancellationToken).ConfigureAwait(false);

        await using var reset = connection.CreateCommand();
        reset.Transaction = transaction;
        reset.CommandText = "UPDATE message_processing SET status = 'queued', started_at_utc = NULL WHERE message_id = $message_id;";
        reset.Parameters.AddWithValue("$message_id", messageId);
        await reset.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureUpdatedAsync(SqliteCommand command, string messageId, CancellationToken cancellationToken)
    {
        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new InvalidOperationException($"Codex task launch '{messageId}' is not actively claimed with the supplied token.");
        }
    }

    private static string FormatDate(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
