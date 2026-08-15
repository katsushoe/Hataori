using FluentAssertions;
using Hataori.Application.Tasks;
using Hataori.Core.Tasks;
using Hataori.Infrastructure.Tasks;
using Microsoft.Data.Sqlite;

namespace Hataori.Infrastructure.Tests.Tasks;

public sealed class SqliteTaskRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Lifecycle_WhenPersisted_UpdatesTaskAndAppendsHistory()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var repository = new SqliteTaskRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);
        var service = new TaskService(repository, TimeProvider.System);

        await service.StartAsync("task-1", "実装", "codex", "conversation-1", "message-1", "概要", "開始", CancellationToken.None);
        await service.HeartbeatAsync("task-1", "SQLite実装", 40, CancellationToken.None);
        await service.CompleteAsync("task-1", "成功", CancellationToken.None);
        var restored = await repository.GetAsync("task-1", CancellationToken.None);

        restored.Should().NotBeNull();
        restored!.Status.Should().Be(HataoriTaskStatus.Completed);
        restored.ProgressPercent.Should().Be(100);
        restored.Result.Should().Be("成功");
        (await CountHistoryAsync(connectionString)).Should().Be(3);
    }

    [Fact]
    public async Task TaskFunctions_WhenUsed_PersistListTerminalStatesHistoryAndRelations()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var repository = new SqliteTaskRepository(connectionString);
        await repository.InitializeAsync(CancellationToken.None);
        var service = new TaskService(repository, TimeProvider.System);
        await service.StartAsync("task-1", "親", "codex", null, null, "概要", "開始", CancellationToken.None);
        await service.StartAsync("task-2", "子", "claude-code", null, null, "概要", "開始", CancellationToken.None);
        await service.StartAsync("task-3", "失敗", "codex", null, null, "概要", "開始", CancellationToken.None);
        await service.StartAsync("task-4", "期限切れ", "codex", null, null, "概要", "開始", CancellationToken.None);

        await service.CancelAsync("task-1", "中止", CancellationToken.None);
        await service.FailAsync("task-3", "異常終了", CancellationToken.None);
        await service.ExpireAsync("task-4", CancellationToken.None);
        await service.AddRelationAsync("task-1", "task-2", "depends_on", CancellationToken.None);

        (await service.ListAsync(HataoriTaskStatus.Cancelled, "codex", CancellationToken.None)).Should().ContainSingle(x => x.TaskId == "task-1");
        (await repository.GetAsync("task-3", CancellationToken.None))!.Status.Should().Be(HataoriTaskStatus.Failed);
        (await repository.GetAsync("task-4", CancellationToken.None))!.Status.Should().Be(HataoriTaskStatus.Expired);
        (await service.GetHistoryAsync("task-1", CancellationToken.None)).Select(x => x.EventType).Should().Equal("started", "cancelled");
        (await service.GetRelationsAsync("task-2", CancellationToken.None)).Should().ContainSingle().Which.RelationType.Should().Be("depends_on");
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static async Task<long> CountHistoryAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM task_history;";
        return (long)(await command.ExecuteScalarAsync(CancellationToken.None) ?? 0L);
    }
}
