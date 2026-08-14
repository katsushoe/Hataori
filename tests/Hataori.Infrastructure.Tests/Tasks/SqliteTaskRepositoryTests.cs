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
