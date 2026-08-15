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

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
