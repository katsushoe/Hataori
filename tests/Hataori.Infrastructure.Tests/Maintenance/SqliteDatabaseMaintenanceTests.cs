using FluentAssertions;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Tasks;
using Hataori.Infrastructure.Maintenance;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Tasks;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Maintenance;

public sealed class SqliteDatabaseMaintenanceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-maintenance-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExecuteAsync_ExpiresAndPurgesOnlyEligibleRows()
    {
        var now = new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true }.ToString();
        var tasks = new SqliteTaskRepository(connectionString);
        var runs = new SqliteAgentRunRepository(connectionString);
        var messages = new SqliteMessageQueueRepository(connectionString);
        await tasks.InitializeAsync(CancellationToken.None);
        await runs.InitializeAsync(CancellationToken.None);
        await messages.InitializeAsync(CancellationToken.None);
        var oldTask = HataoriTask.Start("old", "Old", "codex", null, null, string.Empty, string.Empty, now.AddDays(-100));
        oldTask.Complete("done", now.AddDays(-100));
        var staleTask = HataoriTask.Start("stale", "Stale", "codex", null, null, string.Empty, string.Empty, now.AddDays(-2));
        await tasks.AddAsync(oldTask, CancellationToken.None);
        await tasks.AddAsync(staleTask, CancellationToken.None);
        var run = AgentRun.Queue("run-old", "message-run", "conversation", "codex", now.AddDays(-40));
        run.MarkStarting();
        run.MarkRunning(1234, now.AddDays(-40));
        run.Complete("session", 0, null, now.AddDays(-40));
        await runs.AddAsync(run, CancellationToken.None);
        var message = new IncomingMessage("message-old", "conversation", "codex", Directory.GetCurrentDirectory(), "sender", null, "prompt", "body", null, now.AddDays(-40));
        await messages.EnqueueAsync(message, 0, CancellationToken.None);
        await messages.MarkRunningAsync(message.MessageId, CancellationToken.None);
        await messages.MarkRespondedAsync(message.MessageId, "reply", now.AddDays(-40), CancellationToken.None);
        var maintenance = new SqliteDatabaseMaintenance(connectionString, new FixedTimeProvider(now));

        var result = await maintenance.ExecuteAsync(new DatabaseMaintenanceSettings(TimeSpan.FromDays(1), TimeSpan.FromDays(90), TimeSpan.FromDays(30), TimeSpan.FromDays(30), true), CancellationToken.None);

        result.Should().Be(new DatabaseMaintenanceResult(1, 1, 1, 1, true));
        (await tasks.GetAsync("old", CancellationToken.None)).Should().BeNull();
        (await tasks.GetAsync("stale", CancellationToken.None))!.Status.Should().Be(HataoriTaskStatus.Expired);
        (await runs.GetAsync("run-old", CancellationToken.None)).Should().BeNull();
        (await messages.GetProcessingStatusAsync("message-old", CancellationToken.None)).Should().BeNull();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
