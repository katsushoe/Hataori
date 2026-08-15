using FluentAssertions;
using Hataori.Core.Messages;
using Hataori.Infrastructure.Messages;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Messages;

public sealed class SqliteMessageQueueRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-queue-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task EnqueueAsync_DuplicateMessage_PersistsSingleQueueItem()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var message = CreateMessage("message-1", "first");

        var first = await repository.EnqueueAsync(message, 0, CancellationToken.None);
        var duplicate = await repository.EnqueueAsync(message, 0, CancellationToken.None);

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        (await repository.ListAsync(null, CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task TryClaimNextAsync_SamePriority_ClaimsInFifoOrder()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-1", "first"), 0, CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-2", "second", "conversation-2"), 0, CancellationToken.None);

        var first = await repository.TryClaimNextAsync("codex", CancellationToken.None);
        var second = await repository.TryClaimNextAsync("codex", CancellationToken.None);

        first!.Message.MessageId.Should().Be("message-1");
        second!.Message.MessageId.Should().Be("message-2");
        (await repository.ListAsync(null, CancellationToken.None)).Should().BeEmpty();
        (await GetStatusAsync("message-1")).Should().Be("starting");
    }

    [Fact]
    public async Task ListAsync_HigherPriority_ReturnsBeforeEarlierNormalMessage()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("normal", "normal"), 0, CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("urgent", "urgent"), 10, CancellationToken.None);

        var messages = await repository.ListAsync(null, CancellationToken.None);

        messages.Select(item => item.Message.MessageId).Should().Equal("urgent", "normal");
    }

    [Fact]
    public async Task CancelAndRetryAsync_QueuedMessage_RequeuesAtEnd()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-1", "first"), 0, CancellationToken.None);

        await repository.CancelQueuedAsync("message-1", DateTimeOffset.UtcNow, CancellationToken.None);

        (await repository.ListAsync(null, CancellationToken.None)).Should().BeEmpty();
        (await GetStatusAsync("message-1")).Should().Be("cancelled");

        var retried = await repository.RetryAsync("message-1", DateTimeOffset.UtcNow, CancellationToken.None);

        retried.Message.MessageId.Should().Be("message-1");
        (await GetStatusAsync("message-1")).Should().Be("queued");
    }

    [Fact]
    public async Task GetQueuedAsync_ExistingMessage_ReturnsItem()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-1", "first"), 0, CancellationToken.None);

        var result = await repository.GetQueuedAsync("message-1", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Message.Body.Should().Be("first");
    }

    [Fact]
    public async Task InitializeAsync_PreRetrySchema_AddsReplyColumns()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync(CancellationToken.None);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE message_processing (
                    message_id TEXT PRIMARY KEY, conversation_id TEXT NOT NULL, agent_id TEXT NOT NULL,
                    sender_agent_id TEXT NOT NULL, reply_to_message_id TEXT NULL, message_type TEXT NOT NULL,
                    body TEXT NOT NULL, payload_json TEXT NULL, status TEXT NOT NULL, received_at_utc TEXT NOT NULL,
                    started_at_utc TEXT NULL, completed_at_utc TEXT NULL, error TEXT NULL);
                """;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }

        var repository = new SqliteMessageQueueRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);

        await using var verification = new SqliteConnection(connectionString);
        await verification.OpenAsync(CancellationToken.None);
        await using var pragma = verification.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(message_processing);";
        await using var reader = await pragma.ExecuteReaderAsync(CancellationToken.None);
        var columns = new List<string>();
        while (await reader.ReadAsync(CancellationToken.None))
        {
            columns.Add(reader.GetString(1));
        }

        columns.Should().Contain(["reply_attempt_count", "next_reply_attempt_at_utc", "reply_error", "reply_message_id"]);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private SqliteMessageQueueRepository CreateRepository()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        return new SqliteMessageQueueRepository(connectionString);
    }

    [Fact]
    public async Task TryClaimNextAsync_SameConversationRunning_LeavesNextMessageQueued()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-1", "first"), 0, CancellationToken.None);
        await repository.EnqueueAsync(CreateMessage("message-2", "second"), 0, CancellationToken.None);

        var first = await repository.TryClaimNextAsync("codex", CancellationToken.None);
        var blocked = await repository.TryClaimNextAsync("codex", CancellationToken.None);

        first!.Message.MessageId.Should().Be("message-1");
        blocked.Should().BeNull();
        (await repository.ListAsync(null, CancellationToken.None)).Should().ContainSingle()
            .Which.Message.MessageId.Should().Be("message-2");
    }

    private static IncomingMessage CreateMessage(string id, string body, string conversationId = "conversation-1") => new(
        id, conversationId, "codex", "sender", null, "message", body, null, DateTimeOffset.UtcNow);

    private async Task<string?> GetStatusAsync(string messageId)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString());
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT status FROM message_processing WHERE message_id = $message_id;";
        command.Parameters.AddWithValue("$message_id", messageId);
        return (string?)await command.ExecuteScalarAsync(CancellationToken.None);
    }
}
