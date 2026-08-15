using Hataori.Core.Tasks;
using FluentAssertions;

namespace Hataori.Core.Tests.Tasks;

public sealed class HataoriTaskTests
{
    [Fact]
    public void Start_ValidInput_CreatesActiveTask()
    {
        var startedAt = new DateTimeOffset(2026, 8, 14, 1, 2, 3, TimeSpan.Zero);

        var task = HataoriTask.Start("task-1", "実装", "codex", "conversation-1", "message-1", "概要", "開始", startedAt);

        task.Status.Should().Be(HataoriTaskStatus.Active);
        task.ProgressPercent.Should().Be(0);
        task.StartedAtUtc.Should().Be(startedAt);
        task.LastActivityAtUtc.Should().Be(startedAt);
    }

    [Fact]
    public void Heartbeat_ValidProgress_UpdatesCurrentWorkAndActivity()
    {
        var task = CreateTask();
        var occurredAt = new DateTimeOffset(2026, 8, 14, 2, 0, 0, TimeSpan.Zero);

        task.Heartbeat("ドメイン実装", 25, occurredAt);

        task.CurrentWork.Should().Be("ドメイン実装");
        task.ProgressPercent.Should().Be(25);
        task.LastActivityAtUtc.Should().Be(occurredAt);
    }

    [Fact]
    public void Complete_ActiveTask_MarksTaskCompleted()
    {
        var task = CreateTask();
        var occurredAt = new DateTimeOffset(2026, 8, 14, 3, 0, 0, TimeSpan.Zero);

        task.Complete("成功", occurredAt);

        task.Status.Should().Be(HataoriTaskStatus.Completed);
        task.ProgressPercent.Should().Be(100);
        task.Result.Should().Be("成功");
        task.CompletedAtUtc.Should().Be(occurredAt);
    }

    [Fact]
    public void Heartbeat_CompletedTask_ThrowsInvalidOperationException()
    {
        var task = CreateTask();
        task.Complete("成功", DateTimeOffset.UtcNow);

        var act = () => task.Heartbeat("再開", 50, DateTimeOffset.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(HataoriTaskStatus.Cancelled)]
    [InlineData(HataoriTaskStatus.Failed)]
    [InlineData(HataoriTaskStatus.Expired)]
    public void End_ValidTerminalStatus_UpdatesStatus(HataoriTaskStatus status)
    {
        var task = CreateTask();

        task.End(status, "終了", DateTimeOffset.UtcNow);

        task.Status.Should().Be(status);
        task.CompletedAtUtc.Should().NotBeNull();
    }

    private static HataoriTask CreateTask()
    {
        return HataoriTask.Start("task-1", "実装", "codex", null, null, "概要", "開始", DateTimeOffset.UtcNow);
    }
}
