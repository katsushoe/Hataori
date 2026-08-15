using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Agents;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Sessions;
using Microsoft.Data.Sqlite;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class ActivationManagerTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-activation-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ProcessNextAsync_NewConversation_StartsDriverAndPersistsRunAndSession()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Queue.EnqueueAsync(CreateMessage("message-1"), 0, CancellationToken.None);
        ConfigureSuccessfulStart(fixture.Driver, "native-1", "done");

        var result = await fixture.Manager.ProcessNextAsync(CreateRequest(), CancellationToken.None);

        result!.Succeeded.Should().BeTrue();
        (await fixture.Sessions.GetAsync("conversation-1", "codex", CancellationToken.None))!.NativeSessionId.Should().Be("native-1");
        (await fixture.Runs.ListAsync(AgentRunStatus.Completed, "codex", CancellationToken.None)).Should().ContainSingle()
            .Which.FinalMessage.Should().Be("done");
        await fixture.Driver.Received(1).StartAsync(
            Arg.Is<AgentDriverRequest>(request =>
                request.Environment["HATAORI_MESSAGE_ID"] == "message-1" &&
                request.Environment["HATAORI_CONVERSATION_ID"] == "conversation-1" &&
                request.Environment["HATAORI_AGENT_ID"] == "codex"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_ExistingConversation_ResumesAndUpdatesSession()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Sessions.RegisterAsync("conversation-1", "codex", "native-old", CancellationToken.None);
        await fixture.Queue.EnqueueAsync(CreateMessage("message-2"), 0, CancellationToken.None);
        ConfigureSuccessfulResume(fixture.Driver, "native-new", "resumed");

        var result = await fixture.Manager.ProcessNextAsync(CreateRequest(), CancellationToken.None);

        result!.Succeeded.Should().BeTrue();
        (await fixture.Sessions.GetAsync("conversation-1", "codex", CancellationToken.None))!.NativeSessionId.Should().Be("native-new");
        await fixture.Driver.Received(1).ResumeAsync("native-old", Arg.Any<AgentDriverRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessNextAsync_ResumeFailure_FailsRunAndInvalidatesSession()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Sessions.RegisterAsync("conversation-1", "codex", "native-old", CancellationToken.None);
        await fixture.Queue.EnqueueAsync(CreateMessage("message-3"), 0, CancellationToken.None);
        fixture.Driver.ResumeAsync(Arg.Any<string>(), Arg.Any<AgentDriverRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AgentDriverResult>>(_ => throw new InvalidOperationException("resume failed"));

        var result = await fixture.Manager.ProcessNextAsync(CreateRequest(), CancellationToken.None);

        result!.Succeeded.Should().BeFalse();
        (await fixture.Sessions.GetAsync("conversation-1", "codex", CancellationToken.None))!.Status.Should().Be(ConversationSessionStatus.Invalid);
        (await fixture.Runs.ListAsync(AgentRunStatus.Failed, "codex", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessNextAsync_AgentLane_ClaimsOnlyMatchingAgent()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Queue.EnqueueAsync(CreateMessage("claude-message", "claude-code"), 10, CancellationToken.None);
        await fixture.Queue.EnqueueAsync(CreateMessage("codex-message"), 0, CancellationToken.None);
        ConfigureSuccessfulStart(fixture.Driver, "native-1", "done");

        var result = await fixture.Manager.ProcessNextAsync(CreateRequest(), "codex", CancellationToken.None);

        result!.MessageId.Should().Be("codex-message");
        (await fixture.Queue.ListAsync("claude-code", CancellationToken.None)).Should().ContainSingle()
            .Which.Message.MessageId.Should().Be("claude-message");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private async Task<Fixture> CreateFixtureAsync()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var queue = new SqliteMessageQueueRepository(connectionString);
        var sessionRepository = new SqliteConversationSessionRepository(connectionString);
        var runRepository = new SqliteAgentRunRepository(connectionString);
        await queue.InitializeAsync(CancellationToken.None);
        await sessionRepository.InitializeAsync(CancellationToken.None);
        await runRepository.InitializeAsync(CancellationToken.None);
        var sessions = new ConversationSessionService(sessionRepository, TimeProvider.System);
        var runs = new AgentRunService(runRepository, TimeProvider.System);
        var driver = Substitute.For<IAgentDriver>();
        driver.AgentType.Returns("codex");
        var manager = new ActivationManager(queue, new ConversationMutex(), sessions, runs, [driver], TimeProvider.System);
        return new Fixture(manager, queue, sessions, runs, driver);
    }

    private static void ConfigureSuccessfulStart(IAgentDriver driver, string sessionId, string finalMessage)
    {
        driver.StartAsync(Arg.Any<AgentDriverRequest>(), Arg.Any<CancellationToken>()).Returns(call => CompleteAsync(call.Arg<AgentDriverRequest>(), sessionId, finalMessage));
    }

    private static void ConfigureSuccessfulResume(IAgentDriver driver, string sessionId, string finalMessage)
    {
        driver.ResumeAsync(Arg.Any<string>(), Arg.Any<AgentDriverRequest>(), Arg.Any<CancellationToken>()).Returns(call => CompleteAsync(call.Arg<AgentDriverRequest>(), sessionId, finalMessage));
    }

    private static async Task<AgentDriverResult> CompleteAsync(AgentDriverRequest request, string sessionId, string finalMessage)
    {
        await request.ProcessStarted!(4321, CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        return new AgentDriverResult(sessionId, finalMessage, new AgentProcessResult(4321, 0, string.Empty, string.Empty, false, false, now, now));
    }

    private static IncomingMessage CreateMessage(string messageId, string agentId = "codex") => new(
        messageId, "conversation-1", agentId, "sender", null, "message", "work", null, DateTimeOffset.UtcNow);

    private static ActivationRequest CreateRequest() => new(Directory.GetCurrentDirectory(), Directory.GetCurrentDirectory(), "http://127.0.0.1:45440/mcp");

    private sealed record Fixture(
        ActivationManager Manager,
        SqliteMessageQueueRepository Queue,
        ConversationSessionService Sessions,
        AgentRunService Runs,
        IAgentDriver Driver);
}
