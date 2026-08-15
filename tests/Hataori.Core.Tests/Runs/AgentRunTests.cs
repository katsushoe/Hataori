using FluentAssertions;
using Hataori.Core.Runs;

namespace Hataori.Core.Tests.Runs;

public sealed class AgentRunTests
{
    [Fact]
    public void Lifecycle_QueueRunComplete_RecordsExecutionResult()
    {
        var now = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now);

        run.MarkStarting();
        run.MarkRunning(1234, now.AddSeconds(1));
        run.Complete("session-1", 0, "done", now.AddSeconds(2));

        run.Status.Should().Be(AgentRunStatus.Completed);
        run.ProcessId.Should().Be(1234);
        run.NativeSessionId.Should().Be("session-1");
        run.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Complete_WithNonZeroExitCode_Throws()
    {
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", DateTimeOffset.UtcNow);
        run.MarkStarting();
        run.MarkRunning(1234, DateTimeOffset.UtcNow);

        var action = () => run.Complete("session-1", 1, null, DateTimeOffset.UtcNow);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cancel_WhenQueued_TransitionsToCancelled()
    {
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", DateTimeOffset.UtcNow);

        run.Cancel(DateTimeOffset.UtcNow);

        run.Status.Should().Be(AgentRunStatus.Cancelled);
        run.EndedAtUtc.Should().NotBeNull();
    }
}
