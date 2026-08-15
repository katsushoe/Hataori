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
