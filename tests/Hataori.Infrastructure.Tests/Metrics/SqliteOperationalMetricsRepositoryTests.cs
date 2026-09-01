using FluentAssertions;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Metrics;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Tasks;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Metrics;

public sealed class SqliteOperationalMetricsRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-metrics-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetAsync_AggregatesOnlyRequestedWorkspace()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        await new SqliteTaskRepository(connectionString).InitializeAsync(CancellationToken.None);
        await new SqliteMessageQueueRepository(connectionString).InitializeAsync(CancellationToken.None);
        await new SqliteAgentRunRepository(connectionString).InitializeAsync(CancellationToken.None);
        await SeedAsync(connectionString);
        var now = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);
        var repository = new SqliteOperationalMetricsRepository(connectionString, new FixedTimeProvider(now));

        var metrics = await repository.GetAsync("alpha", CancellationToken.None);

        metrics.GeneratedAtUtc.Should().Be(now);
        metrics.Tasks.Total.Should().Be(2);
        metrics.Tasks.Completed.Should().Be(1);
        metrics.Tasks.Failed.Should().Be(1);
        metrics.Tasks.CompletionRatePercent.Should().Be(50);
        metrics.Tasks.AverageDurationSeconds.Should().Be(90);
        metrics.Messages.Total.Should().Be(2);
        metrics.Messages.Responded.Should().Be(1);
        metrics.Messages.PendingReplyRetries.Should().Be(1);
        metrics.Messages.ReplyAttempts.Should().Be(2);
        metrics.AgentRuns.Total.Should().Be(2);
        metrics.AgentRuns.SuccessRatePercent.Should().Be(50);
        metrics.AgentRuns.AverageQueueWaitSeconds.Should().Be(15);
        metrics.AgentRuns.AverageRunDurationSeconds.Should().Be(45);
        metrics.Agents.Should().ContainSingle().Which.AgentId.Should().Be("claude-code");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private static async Task SeedAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks VALUES
              ('task-1','alpha','One','codex',NULL,NULL,'completed','s','w',100,'2026-09-01T00:00:00+00:00','2026-09-01T00:01:00+00:00','2026-09-01T00:01:00+00:00','ok'),
              ('task-2','alpha','Two','codex',NULL,NULL,'failed','s','w',50,'2026-09-01T00:00:00+00:00','2026-09-01T00:02:00+00:00','2026-09-01T00:02:00+00:00','bad'),
              ('task-3','beta','Other','codex',NULL,NULL,'completed','s','w',100,'2026-09-01T00:00:00+00:00','2026-09-01T00:10:00+00:00','2026-09-01T00:10:00+00:00','ok');
            INSERT INTO message_processing (workspace_id,message_id,conversation_id,agent_id,working_directory,sender_agent_id,message_type,body,status,received_at_utc,started_at_utc,completed_at_utc,reply_attempt_count,next_reply_attempt_at_utc)
            VALUES
              ('alpha','m1','c1','claude-code','','sender','task','body','responded','2026-09-01T00:00:00+00:00','2026-09-01T00:00:10+00:00','2026-09-01T00:01:00+00:00',0,NULL),
              ('alpha','m2','c2','claude-code','','sender','task','body','failed','2026-09-01T00:00:00+00:00','2026-09-01T00:00:20+00:00','2026-09-01T00:02:00+00:00',2,'2026-09-01T00:03:00+00:00');
            INSERT INTO agent_runs VALUES
              ('alpha','r1','m1','c1','claude-code',NULL,NULL,'completed','2026-09-01T00:00:00+00:00','2026-09-01T00:00:10+00:00','2026-09-01T00:00:40+00:00',0,'ok',NULL),
              ('alpha','r2','m2','c2','claude-code',NULL,NULL,'failed','2026-09-01T00:00:00+00:00','2026-09-01T00:00:20+00:00','2026-09-01T00:01:20+00:00',1,NULL,'bad');
            """;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
