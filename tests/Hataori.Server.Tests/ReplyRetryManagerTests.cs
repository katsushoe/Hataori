using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Itoguruma;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Microsoft.Data.Sqlite;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class ReplyRetryManagerTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-reply-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ProcessDueAsync_PendingReply_SendsWithStableKeyAndMarksResponded()
    {
        var fixture = await CreateFixtureAsync(maxAttempts: 3);
        var now = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        await PrepareCompletedRunAsync(fixture.Queue, fixture.Runs, now);
        await fixture.Manager.ScheduleInitialFailureAsync("message-1", "offline", now, CancellationToken.None);
        fixture.Itoguruma.ReplyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("reply-1");

        var result = await fixture.Manager.ProcessDueAsync(now.AddSeconds(1), CancellationToken.None);

        result.Succeeded.Should().Be(1);
        (await fixture.Queue.GetProcessingStatusAsync("message-1", CancellationToken.None)).Should().Be(MessageProcessingStatus.Responded);
        await fixture.Itoguruma.Received(1).ReplyAsync(
            "sender", "final", "conversation-1", "message-1", "hataori-reply:message-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_LastFailure_ExhaustsRetryAndStopsScheduling()
    {
        var fixture = await CreateFixtureAsync(maxAttempts: 2);
        var now = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        await PrepareCompletedRunAsync(fixture.Queue, fixture.Runs, now);
        await fixture.Manager.ScheduleInitialFailureAsync("message-1", "offline", now, CancellationToken.None);
        fixture.Itoguruma.ReplyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("still offline"));

        var first = await fixture.Manager.ProcessDueAsync(now.AddSeconds(1), CancellationToken.None);
        var later = await fixture.Manager.ProcessDueAsync(now.AddHours(1), CancellationToken.None);

        first.Exhausted.Should().Be(1);
        later.Should().Be(new ReplyRetryBatchResult(0, 0, 0));
        (await fixture.Queue.GetProcessingStatusAsync("message-1", CancellationToken.None)).Should().Be(MessageProcessingStatus.Failed);
        await fixture.Itoguruma.Received(1).ReplyAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<Fixture> CreateFixtureAsync(int maxAttempts)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var queue = new SqliteMessageQueueRepository(connectionString);
        var runs = new SqliteAgentRunRepository(connectionString);
        await queue.InitializeAsync(CancellationToken.None);
        await runs.InitializeAsync(CancellationToken.None);
        var itoguruma = Substitute.For<IItogurumaClient>();
        var settings = new ReplyRetrySettings(maxAttempts, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(8), 10);
        return new Fixture(queue, runs, itoguruma, new ReplyRetryManager(queue, itoguruma, settings));
    }

    private static async Task PrepareCompletedRunAsync(SqliteMessageQueueRepository queue, SqliteAgentRunRepository runs, DateTimeOffset now)
    {
        var message = new IncomingMessage("message-1", "conversation-1", "codex", "sender", null, "message", "work", null, now);
        await queue.EnqueueAsync(message, 0, CancellationToken.None);
        await queue.TryClaimNextAsync("codex", CancellationToken.None);
        await queue.MarkRunningAsync("message-1", CancellationToken.None);
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now);
        run.MarkStarting();
        run.MarkRunning(1234, now);
        run.Complete("session-1", 0, "final", now);
        await runs.AddAsync(run, CancellationToken.None);
    }

    private sealed record Fixture(
        SqliteMessageQueueRepository Queue,
        SqliteAgentRunRepository Runs,
        IItogurumaClient Itoguruma,
        ReplyRetryManager Manager);
}
