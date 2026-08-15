using FluentAssertions;
using Hataori.Core.Runs;
using Hataori.Infrastructure.Runs;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Runs;

public sealed class SqliteAgentRunRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-run-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Lifecycle_WhenSaved_RestoresCompletedRun()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync(CancellationToken.None);
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", DateTimeOffset.UtcNow);
        await repository.AddAsync(run, CancellationToken.None);

        run.MarkStarting();
        run.MarkRunning(1234, DateTimeOffset.UtcNow);
        run.Complete("session-1", 0, "done", DateTimeOffset.UtcNow);
        await repository.SaveAsync(run, CancellationToken.None);

        var restored = await repository.GetAsync("run-1", CancellationToken.None);
        restored.Should().NotBeNull();
        restored!.Status.Should().Be(AgentRunStatus.Completed);
        restored.FinalMessage.Should().Be("done");
        (await repository.ListAsync(AgentRunStatus.Completed, "codex", CancellationToken.None)).Should().ContainSingle();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private SqliteAgentRunRepository CreateRepository()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        return new SqliteAgentRunRepository(connectionString);
    }
}
