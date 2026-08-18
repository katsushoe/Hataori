using FluentAssertions;
using Hataori.Application.Tasks;
using Hataori.Infrastructure.Tasks;
using Microsoft.Data.Sqlite;

namespace Hataori.Server.Tests;

public sealed class TaskMcpToolsTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-mcp-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Tools_TaskLifecycle_UsesTaskApplicationService()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var repository = new SqliteTaskRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);
        var tools = new TaskMcpTools(new TaskService(repository, TimeProvider.System));

        await tools.StartAsync("task-1", "MCP", "codex", null, null, "概要", "開始", CancellationToken.None);
        await tools.HeartbeatAsync("task-1", "実装", 60, CancellationToken.None);
        var completed = await tools.CompleteAsync("task-1", "成功", CancellationToken.None);

        completed.ProgressPercent.Should().Be(100);
        (await tools.HistoryAsync("task-1", CancellationToken.None)).Should().HaveCount(3);
        (await tools.ListAsync(null, "codex", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task FindConflictsAsync_OverlappingKeyword_ReturnsOtherAgentsTaskAndExcludesOwn()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var repository = new SqliteTaskRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);
        var tools = new TaskMcpTools(new TaskService(repository, TimeProvider.System));
        await tools.StartAsync("task-codex", "認証処理を修正", "codex", null, null, "ログイン周りの改修", "調査中", CancellationToken.None);
        await tools.StartAsync("task-claude-own", "認証処理のドキュメント", "claude-code", null, null, "認証仕様の記述", "執筆中", CancellationToken.None);
        await tools.StartAsync("task-unrelated", "READMEを更新", "codex", null, null, "誤字修正", "作業中", CancellationToken.None);

        var conflicts = await tools.FindConflictsAsync("認証処理のバグ修正", "ログイン画面の不具合対応", "claude-code", CancellationToken.None);

        conflicts.Should().ContainSingle().Which.TaskId.Should().Be("task-codex");
    }

    [Fact]
    public async Task FindConflictsAsync_NoOverlap_ReturnsEmpty()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var repository = new SqliteTaskRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);
        var tools = new TaskMcpTools(new TaskService(repository, TimeProvider.System));
        await tools.StartAsync("task-unrelated", "READMEを更新", "codex", null, null, "誤字修正", "作業中", CancellationToken.None);

        var conflicts = await tools.FindConflictsAsync("データベース移行スクリプト", "SQLiteのMigration追加", null, CancellationToken.None);

        conflicts.Should().BeEmpty();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
