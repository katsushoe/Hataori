using FluentAssertions;
using Hataori.Core.Messages;
using Hataori.Infrastructure.Codex;
using Hataori.Infrastructure.Messages;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Codex;

public sealed class SqliteCodexTaskLaunchRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-codex-launch-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ClaimStarted_RemovesMessageFromQueueAndPreventsDuplicateClaim()
    {
        var (queue, launches) = await CreateAsync();
        await EnqueueAsync(queue, "msg-1");
        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");

        var claimed = await launches.TryClaimAsync(now, now.AddMinutes(5), CancellationToken.None);

        claimed.Should().NotBeNull();
        claimed!.ProjectName.Should().Be("CRs");
        claimed.Prompt.Should().Be("残りのCRをリストアップして");
        await launches.MarkStartedAsync(claimed.MessageId, claimed.ClaimToken, "thread-1", now.AddSeconds(1), CancellationToken.None);
        (await queue.GetQueuedAsync("msg-1", CancellationToken.None)).Should().BeNull();
        (await launches.TryClaimAsync(now.AddSeconds(2), now.AddMinutes(6), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ExpiredClaim_CanBeClaimedAgainWithNewToken()
    {
        var (queue, launches) = await CreateAsync();
        await EnqueueAsync(queue, "msg-2");
        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");
        var first = await launches.TryClaimAsync(now, now.AddSeconds(30), CancellationToken.None);

        var second = await launches.TryClaimAsync(now.AddSeconds(31), now.AddMinutes(1), CancellationToken.None);

        second.Should().NotBeNull();
        second!.ClaimToken.Should().NotBe(first!.ClaimToken);
    }

    [Fact]
    public async Task Release_MakesRequestImmediatelyClaimable()
    {
        var (queue, launches) = await CreateAsync();
        await EnqueueAsync(queue, "msg-3");
        var now = DateTimeOffset.Parse("2026-08-23T00:00:00Z");
        var first = await launches.TryClaimAsync(now, now.AddMinutes(5), CancellationToken.None);

        await launches.ReleaseAsync(first!.MessageId, first.ClaimToken, "project not found", now.AddSeconds(1), CancellationToken.None);
        var second = await launches.TryClaimAsync(now.AddSeconds(2), now.AddMinutes(5), CancellationToken.None);

        second.Should().NotBeNull();
        second!.ClaimToken.Should().NotBe(first.ClaimToken);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<(SqliteMessageQueueRepository Queue, SqliteCodexTaskLaunchRepository Launches)> CreateAsync()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var queue = new SqliteMessageQueueRepository(connectionString);
        var launches = new SqliteCodexTaskLaunchRepository(connectionString);
        await queue.InitializeAsync(CancellationToken.None);
        await launches.InitializeAsync(CancellationToken.None);
        return (queue, launches);
    }

    private static Task<bool> EnqueueAsync(SqliteMessageQueueRepository queue, string messageId) =>
        queue.EnqueueAsync(new IncomingMessage(messageId, "thread-1", "codex", Path.Combine("F:\\Workspace\\Projects", "CRs"), "sender", null, "message", "残りのCRをリストアップして", null, DateTimeOffset.UtcNow), 0, CancellationToken.None);
}
