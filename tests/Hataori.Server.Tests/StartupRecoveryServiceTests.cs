using FluentAssertions;
using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Application.Tasks;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class StartupRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_MissingProcess_FailsLinkedStateIdempotently()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now.AddMinutes(-5));
        run.MarkStarting();
        run.MarkRunning(1234, now.AddMinutes(-5));
        var session = ConversationSession.Create("conversation-1", "codex", "session-1", now.AddHours(-1));
        session.StartRun(now.AddMinutes(-5));
        var runRepository = Substitute.For<IAgentRunRepository>();
        var sessionRepository = Substitute.For<IConversationSessionRepository>();
        var messages = Substitute.For<IMessageQueueRepository>();
        var probe = Substitute.For<IAgentProcessProbe>();
        runRepository.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(_ => new[] { run });
        runRepository.GetAsync(run.RunId, Arg.Any<CancellationToken>()).Returns(_ => run);
        sessionRepository.ListAsync(ConversationSessionStatus.Running, null, Arg.Any<CancellationToken>()).Returns(_ => session.Status == ConversationSessionStatus.Running ? new[] { session } : []);
        sessionRepository.GetAsync(session.ConversationId, session.AgentId, Arg.Any<CancellationToken>()).Returns(_ => session);
        messages.GetProcessingStatusAsync(run.MessageId, Arg.Any<CancellationToken>()).Returns(MessageProcessingStatus.Running);
        probe.IsRunning(1234, run.StartedAtUtc).Returns(false);
        var service = CreateService(runRepository, sessionRepository, messages, probe, now);

        var first = await service.RecoverAsync(CancellationToken.None);
        var second = await service.RecoverAsync(CancellationToken.None);

        first.Should().Be(new StartupRecoveryResult(1, 1, 1, 0));
        second.Should().Be(new StartupRecoveryResult(0, 0, 0, 0));
        run.Status.Should().Be(AgentRunStatus.Failed);
        session.Status.Should().Be(ConversationSessionStatus.Invalid);
        await messages.Received(1).MarkFailedAsync(run.MessageId, Arg.Any<string>(), now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecoverAsync_LiveProcess_PreservesRunAndSession()
    {
        var now = DateTimeOffset.UtcNow;
        var run = AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", now.AddMinutes(-5));
        run.MarkStarting();
        run.MarkRunning(1234, now.AddMinutes(-5));
        var session = ConversationSession.Create("conversation-1", "codex", "session-1", now.AddHours(-1));
        session.StartRun(now.AddMinutes(-5));
        var runRepository = Substitute.For<IAgentRunRepository>();
        var sessionRepository = Substitute.For<IConversationSessionRepository>();
        var messages = Substitute.For<IMessageQueueRepository>();
        var probe = Substitute.For<IAgentProcessProbe>();
        runRepository.ListAsync(null, null, Arg.Any<CancellationToken>()).Returns(new[] { run });
        sessionRepository.ListAsync(ConversationSessionStatus.Running, null, Arg.Any<CancellationToken>()).Returns(new[] { session });
        probe.IsRunning(1234, run.StartedAtUtc).Returns(true);
        var service = CreateService(runRepository, sessionRepository, messages, probe, now);

        var result = await service.RecoverAsync(CancellationToken.None);

        result.Should().Be(new StartupRecoveryResult(0, 0, 0, 1));
        run.Status.Should().Be(AgentRunStatus.Running);
        session.Status.Should().Be(ConversationSessionStatus.Running);
        await messages.DidNotReceiveWithAnyArgs().MarkFailedAsync(default!, default!, default, default);
    }

    private static StartupRecoveryService CreateService(IAgentRunRepository runs, IConversationSessionRepository sessions, IMessageQueueRepository messages, IAgentProcessProbe probe, DateTimeOffset now)
    {
        var timeProvider = new FixedTimeProvider(now);
        return new StartupRecoveryService(Substitute.For<ITaskRepository>(), new AgentRunService(runs, timeProvider), runs, new ConversationSessionService(sessions, timeProvider), sessions, messages, probe, timeProvider);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
