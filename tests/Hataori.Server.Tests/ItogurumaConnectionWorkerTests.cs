using Hataori.Application.Activation;
using Hataori.Application.Agents;
using Hataori.Application.Itoguruma;
using Hataori.Application.Messages;
using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class ItogurumaConnectionWorkerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hataori-project-poll-{Guid.NewGuid():N}");

    [Fact]
    public async Task PollProjectsAsync_UnregisteredProjects_RegistersInboxesAndPollsEveryProject()
    {
        Directory.CreateDirectory(Path.Combine(_root, "CRs"));
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var client = CreateClient();
        var worker = CreateWorker(client, Substitute.For<IMessageQueueRepository>(), SingleRoot());

        await worker.PollProjectsAsync(CancellationToken.None);

        await client.Received(1).RegisterProjectInboxAsync("crs", "CRs", Arg.Any<CancellationToken>());
        await client.Received(1).RegisterProjectInboxAsync("hataori", "Hataori", Arg.Any<CancellationToken>());
        await client.Received(1).GetMessagesAsync("crs", 50, 300, Arg.Is<string?>(value => value == null), Arg.Any<CancellationToken>());
        await client.Received(1).GetMessagesAsync("hataori", 50, 300, Arg.Is<string?>(value => value == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_RegisteredProject_KeepsDisplayNameAndPolls()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var client = CreateClient(new ItogurumaProject("hataori", "Hataori Custom", "hataori", true));
        var worker = CreateWorker(client, Substitute.For<IMessageQueueRepository>(), SingleRoot());

        await worker.PollProjectsAsync(CancellationToken.None);

        await client.DidNotReceive().RegisterProjectInboxAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await client.Received(1).GetMessagesAsync("hataori", 50, 300, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_DisabledProject_IsNotPolled()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Manga"));
        var client = CreateClient(new ItogurumaProject("manga", "Manga", "manga", false));
        var worker = CreateWorker(client, Substitute.For<IMessageQueueRepository>(), SingleRoot());

        await worker.PollProjectsAsync(CancellationToken.None);

        await client.DidNotReceive().RegisterProjectInboxAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetMessagesAsync("manga", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_ProjectIdOutsideItogurumaRule_IsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Aloe-lang"));
        var client = CreateClient();
        var worker = CreateWorker(client, Substitute.For<IMessageQueueRepository>(), SingleRoot());

        await worker.PollProjectsAsync(CancellationToken.None);

        await client.DidNotReceive().RegisterProjectInboxAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetMessagesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_LeasedMessage_AcknowledgesWithLeaseId()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var client = CreateClient(new ItogurumaProject("hataori", "Hataori", "hataori", true));
        client.GetMessagesAsync("hataori", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([Message("message-1", "lease-1")]);
        client.AcknowledgeAsync("hataori", "message-1", "lease-1", Arg.Any<CancellationToken>()).Returns(true);
        var queue = Substitute.For<IMessageQueueRepository>();
        queue.EnqueueAsync(Arg.Any<Hataori.Core.Messages.IncomingMessage>(), 0, Arg.Any<CancellationToken>()).Returns(true);
        var worker = CreateWorker(client, queue, SingleRoot());

        await worker.PollProjectsAsync(CancellationToken.None);

        await queue.Received(1).EnqueueAsync(Arg.Is<Hataori.Core.Messages.IncomingMessage>(message => message.MessageId == "message-1"), 0, Arg.Any<CancellationToken>());
        await client.Received(1).AcknowledgeAsync("hataori", "message-1", "lease-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_MessageWithoutLeaseId_ThrowsBeforeQueueing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var client = CreateClient(new ItogurumaProject("hataori", "Hataori", "hataori", true));
        client.GetMessagesAsync("hataori", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([Message("message-1", null)]);
        var queue = Substitute.For<IMessageQueueRepository>();
        var worker = CreateWorker(client, queue, SingleRoot());

        var act = () => worker.PollProjectsAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(act);
        await queue.DidNotReceive().EnqueueAsync(Arg.Any<Hataori.Core.Messages.IncomingMessage>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().AcknowledgeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollProjectsAsync_MultipleWorkspaces_QueuesWorkspaceId()
    {
        var secondRoot = Path.Combine(Path.GetTempPath(), $"hataori-project-poll-second-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        Directory.CreateDirectory(Path.Combine(secondRoot, "Kotodama"));
        var client = CreateClient(
            new ItogurumaProject("hataori", "Hataori", "hataori", true),
            new ItogurumaProject("kotodama", "Kotodama", "kotodama", true));
        client.GetMessagesAsync("hataori", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([Message("message-alpha", "lease-alpha")]);
        client.GetMessagesAsync("kotodama", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([Message("message-beta", "lease-beta")]);
        client.AcknowledgeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var queue = Substitute.For<IMessageQueueRepository>();
        queue.EnqueueAsync(Arg.Any<Hataori.Core.Messages.IncomingMessage>(), 0, Arg.Any<CancellationToken>()).Returns(true);
        var worker = CreateWorker(client, queue, new ActivationOptions
        {
            Enabled = true,
            Workspaces =
            [
                new ActivationWorkspaceOptions { WorkspaceId = "alpha", WorkingDirectory = _root },
                new ActivationWorkspaceOptions { WorkspaceId = "beta", WorkingDirectory = secondRoot },
            ],
            ProviderPriority = ["codex"],
            MaxConcurrentRuns = new Dictionary<string, int> { ["codex"] = 1 },
        });

        await worker.PollProjectsAsync(CancellationToken.None);

        await queue.Received(1).EnqueueAsync(
            Arg.Is<Hataori.Core.Messages.IncomingMessage>(message => message.MessageId == "message-alpha" && message.WorkspaceId == "alpha"),
            0, Arg.Any<CancellationToken>());
        await queue.Received(1).EnqueueAsync(
            Arg.Is<Hataori.Core.Messages.IncomingMessage>(message => message.MessageId == "message-beta" && message.WorkspaceId == "beta"),
            0, Arg.Any<CancellationToken>());
        Directory.Delete(secondRoot, true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
        GC.SuppressFinalize(this);
    }

    private static IItogurumaClient CreateClient(params ItogurumaProject[] registered)
    {
        var client = Substitute.For<IItogurumaClient>();
        client.ListProjectsAsync(Arg.Any<CancellationToken>()).Returns(registered);
        client.GetMessagesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ItogurumaMessage>());
        return client;
    }

    private static ItogurumaMessage Message(string messageId, string? leaseId) =>
        new(messageId, "thread", "sender", null, "task", "body", null, null, DateTimeOffset.UtcNow, "leased", null, leaseId);

    private ActivationOptions SingleRoot() => new()
    {
        Enabled = true,
        WorkingDirectory = _root,
        ProviderPriority = ["codex"],
        MaxConcurrentRuns = new Dictionary<string, int> { ["codex"] = 1 },
    };

    private static ItogurumaConnectionWorker CreateWorker(IItogurumaClient client, IMessageQueueRepository queue, ActivationOptions activationOptions)
    {
        var driver = Substitute.For<IAgentDriver>();
        driver.AgentType.Returns("codex");
        var activation = Substitute.For<IOptionsMonitor<ActivationOptions>>();
        activation.CurrentValue.Returns(activationOptions);
        return new ItogurumaConnectionWorker(
            client, queue, new AgentProviderSelector([driver]), new ItogurumaConnectionState(),
            Options.Create(new ItogurumaClientOptions { ReceiveBatchSize = 50, LeaseSeconds = 300 }),
            activation, NullLogger<ItogurumaConnectionWorker>.Instance);
    }
}
