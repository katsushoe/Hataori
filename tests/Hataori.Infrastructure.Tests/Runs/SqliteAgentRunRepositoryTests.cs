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
        var run = AgentRun.Queue("alpha", "run-1", "message-1", "conversation-1", "codex", DateTimeOffset.UtcNow);
        await repository.AddAsync(run, CancellationToken.None);

        run.MarkStarting();
        run.MarkRunning(1234, DateTimeOffset.UtcNow);
        run.Complete("session-1", 0, "done", DateTimeOffset.UtcNow);
        await repository.SaveAsync(run, CancellationToken.None);

        var restored = await repository.GetAsync("run-1", CancellationToken.None);
        restored.Should().NotBeNull();
        restored!.Status.Should().Be(AgentRunStatus.Completed);
        restored.WorkspaceId.Should().Be("alpha");
        restored.FinalMessage.Should().Be("done");
        (await repository.ListAsync(AgentRunStatus.Completed, "codex", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task InitializeAsync_PreWorkspaceSchema_MigratesRunToDefault()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE agent_runs (
                    run_id TEXT PRIMARY KEY, message_id TEXT NOT NULL, conversation_id TEXT NOT NULL, agent_id TEXT NOT NULL,
                    native_session_id TEXT NULL, process_id INTEGER NULL, status TEXT NOT NULL, queued_at_utc TEXT NOT NULL,
                    started_at_utc TEXT NULL, ended_at_utc TEXT NULL, exit_code INTEGER NULL, final_message TEXT NULL, error TEXT NULL);
                INSERT INTO agent_runs VALUES ('legacy', 'message', 'conversation', 'codex', NULL, NULL, 'queued',
                    '2026-01-01T00:00:00.0000000+00:00', NULL, NULL, NULL, NULL, NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteAgentRunRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);

        (await repository.GetAsync("legacy", CancellationToken.None))!.WorkspaceId.Should().Be("default");
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
