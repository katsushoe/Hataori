using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Agents;
using Hataori.Application.Control;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Itoguruma;
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
    public async Task StartupRecoveryGate_Complete_ReleasesDependentsAsReady()
    {
        var gate = new StartupRecoveryGate();

        gate.Complete();

        (await gate.Ready).Should().BeTrue();
    }

    [Fact]
    public async Task StartupRecoveryGate_Fail_ReleasesDependentsWithoutException()
    {
        var gate = new StartupRecoveryGate();

        gate.Fail();

        (await gate.Ready).Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseInitializationGate_Complete_ReleasesDependentsAsReady()
    {
        var gate = new DatabaseInitializationGate();

        gate.Complete();

        (await gate.Ready).Should().BeTrue();
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
        var itogurumaState = new ItogurumaConnectionState();
        itogurumaState.Set("degraded");
        var handler = new ControlCommandHandler(lifetime, TimeProvider.System, tasks, sessions, runs, queue, itogurumaState, CreateActivationManager());

        var response = await handler.HandleAsync(new ControlRequest("monitor"), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Monitor.Should().NotBeNull();
        response.Monitor!.QueueCount.Should().Be(1);
        response.Monitor.Tasks.Single().ProgressPercent.Should().Be(60);
        response.Monitor.Agents.Single().State.Should().Be("running");
        response.Monitor.System.Itoguruma.Should().Be("degraded");
        response.Monitor.System.Sqlite.Should().Be("connected");
    }

    [Fact]
    public async Task Handle_Monitor_WithPopulatedDomainObjects_RoundTripsJson()
    {
        var lifetime = new TestLifetime();
        var tasks = Substitute.For<ITaskRepository>();
        var sessions = Substitute.For<IConversationSessionRepository>();
        var runs = Substitute.For<IAgentRunRepository>();
        var queue = Substitute.For<IMessageQueueRepository>();
        var now = DateTimeOffset.UtcNow;
        tasks.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { HataoriTask.Start("task-1", "Monitor", "codex", "conversation-1", null, "監視", "実行中", now) });
        sessions.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { ConversationSession.Create("conversation-1", "codex", "native-1", now) });
        runs.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now) });
        queue.ListAsync(null, Arg.Any<CancellationToken>()).Returns(Array.Empty<QueuedMessage>());
        var handler = new ControlCommandHandler(lifetime, TimeProvider.System, tasks, sessions, runs, queue, new ItogurumaConnectionState(), CreateActivationManager());
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        };

        var response = await handler.HandleAsync(new ControlRequest("monitor"), CancellationToken.None);
        var json = JsonSerializer.Serialize(response, options);
        var restored = JsonSerializer.Deserialize<ControlResponse>(json, options);

        restored.Should().NotBeNull();
        restored!.Monitor!.Tasks.Should().ContainSingle(task => task.TaskId == "task-1");
        restored.Monitor.Sessions.Should().ContainSingle(session => session.NativeSessionId == "native-1");
        restored.Monitor.Runs.Should().ContainSingle(run => run.RunId == "run-1");
    }

    private static ControlCommandHandler CreateHandler(IHostApplicationLifetime lifetime)
    {
        return new ControlCommandHandler(
            lifetime,
            TimeProvider.System,
            Substitute.For<ITaskRepository>(),
            Substitute.For<IConversationSessionRepository>(),
            Substitute.For<IAgentRunRepository>(),
            Substitute.For<IMessageQueueRepository>(),
            new ItogurumaConnectionState(),
            CreateActivationManager());
    }

    private static ActivationManager CreateActivationManager()
    {
        var queue = Substitute.For<IMessageQueueRepository>();
        var itoguruma = Substitute.For<IItogurumaClient>();
        return new ActivationManager(
            queue,
            Substitute.For<IConversationMutex>(),
            new ConversationSessionService(Substitute.For<IConversationSessionRepository>(), TimeProvider.System),
            new AgentRunService(Substitute.For<IAgentRunRepository>(), TimeProvider.System),
            Array.Empty<IAgentDriver>(),
            TimeProvider.System,
            itoguruma,
            new ReplyRetryManager(queue, itoguruma, new ReplyRetrySettings(3, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), 10)));
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
