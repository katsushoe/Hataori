using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Agents;
using Hataori.Application.Itoguruma;
using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Core.Runs;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Sessions;
using Microsoft.Data.Sqlite;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class AgentRunMcpToolsTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-agent-run-mcp-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task CancelAsync_QueuedRun_DelegatesToActivationManagerAndMarksCancelled()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var runRepository = new SqliteAgentRunRepository(connectionString);
        await runRepository.InitializeAsync(CancellationToken.None);
        var runs = new AgentRunService(runRepository, TimeProvider.System);
        await runs.QueueAsync("run-mcp-1", "message-1", "conversation-1", "codex", CancellationToken.None);
        var manager = CreateActivationManager(runRepository);
        var tools = new AgentRunMcpTools(manager);

        var hadLiveProcess = await tools.CancelAsync("run-mcp-1", CancellationToken.None);

        hadLiveProcess.Should().BeFalse();
        (await runs.GetAsync("run-mcp-1", CancellationToken.None))!.Status.Should().Be(AgentRunStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_UnknownRunId_ThrowsKeyNotFound()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var runRepository = new SqliteAgentRunRepository(connectionString);
        await runRepository.InitializeAsync(CancellationToken.None);
        var tools = new AgentRunMcpTools(CreateActivationManager(runRepository));

        var act = () => tools.CancelAsync("missing-run", CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static ActivationManager CreateActivationManager(IAgentRunRepository runRepository)
    {
        var queue = Substitute.For<IMessageQueueRepository>();
        var itoguruma = Substitute.For<IItogurumaClient>();
        return new ActivationManager(
            queue,
            Substitute.For<IConversationMutex>(),
            new ConversationSessionService(Substitute.For<IConversationSessionRepository>(), TimeProvider.System),
            new AgentRunService(runRepository, TimeProvider.System),
            Array.Empty<IAgentDriver>(),
            TimeProvider.System,
            itoguruma,
            new ReplyRetryManager(queue, itoguruma, new ReplyRetrySettings(3, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), 10)));
    }
}
