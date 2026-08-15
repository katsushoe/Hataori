using FluentAssertions;
using Hataori.Application.Control;
using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Application.Tasks;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Core.Tasks;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class ServerFoundationTests
{
    [Fact]
    public void Validate_MissingDatabasePath_ReturnsFailure()
    {
        var result = new ServerOptionsValidator().Validate(null, new ServerOptions { ControlPipeName = "hataori-test" });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonLoopbackMcpHost_ReturnsFailure()
    {
        var options = new ServerOptions
        {
            DatabasePath = "data/hataori.db",
            ControlPipeName = "hataori-test",
            McpHost = "0.0.0.0",
            McpPort = 45440,
            McpPath = "/mcp",
        };

        var result = new ServerOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void ResolveDatabasePath_RelativePath_UsesApplicationDirectory()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "hataori-server-test");

        var result = ServerPaths.ResolveDatabasePath(Path.Combine("data", "hataori.db"), baseDirectory);

        result.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "data", "hataori.db")));
    }

    [Fact]
    public async Task Handle_Stop_RequestsGracefulShutdown()
    {
        var lifetime = new TestLifetime();
        var handler = CreateHandler(lifetime);

        var response = await handler.HandleAsync(new ControlRequest("stop"), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Status.Should().Be("stopping");
        lifetime.StopRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Reload_ReportsAutomaticReload()
    {
        var lifetime = new TestLifetime();
        var handler = CreateHandler(lifetime);

        var response = await handler.HandleAsync(new ControlRequest("reload"), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Status.Should().Be("reload_on_change_enabled");
        lifetime.StopRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Monitor_ReturnsReadOnlySnapshot()
    {
        var lifetime = new TestLifetime();
        var tasks = Substitute.For<ITaskRepository>();
        var sessions = Substitute.For<IConversationSessionRepository>();
        var runs = Substitute.For<IAgentRunRepository>();
        var queue = Substitute.For<IMessageQueueRepository>();
        var now = DateTimeOffset.UtcNow;
        var task = HataoriTask.Start("task-1", "Monitor", "codex", "conversation-1", null, "監視", "実行中", now);
        task.Heartbeat("表示更新", 60, now);
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now);
        run.MarkStarting();
        run.MarkRunning(1234, now);
        var message = new IncomingMessage("message-2", "conversation-2", "codex", "sender", null, "prompt", "body", null, now);
        var queued = new QueuedMessage(1, 1, 0, message, now);
        tasks.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { task });
        sessions.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(Array.Empty<ConversationSession>());
        runs.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { run });
        queue.ListAsync(null, Arg.Any<CancellationToken>()).Returns(new[] { queued });
        var handler = new ControlCommandHandler(lifetime, TimeProvider.System, tasks, sessions, runs, queue);

        var response = await handler.HandleAsync(new ControlRequest("monitor"), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Monitor.Should().NotBeNull();
        response.Monitor!.QueueCount.Should().Be(1);
        response.Monitor.Tasks.Single().ProgressPercent.Should().Be(60);
        response.Monitor.Agents.Single().State.Should().Be("running");
        response.Monitor.System.Sqlite.Should().Be("connected");
    }

    private static ControlCommandHandler CreateHandler(IHostApplicationLifetime lifetime)
    {
        return new ControlCommandHandler(
            lifetime,
            TimeProvider.System,
            Substitute.For<ITaskRepository>(),
            Substitute.For<IConversationSessionRepository>(),
            Substitute.For<IAgentRunRepository>(),
            Substitute.For<IMessageQueueRepository>());
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopRequested = true;
    }
}
