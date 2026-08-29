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
    public async Task PollProjectsAsync_MultipleProjects_RegistersAndPollsEveryProject()
    {
        Directory.CreateDirectory(Path.Combine(_root, "CRs"));
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var client = Substitute.For<IItogurumaClient>();
        client.GetMessagesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Is<string?>(value => value == null), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ItogurumaMessage>());
        var queue = Substitute.For<IMessageQueueRepository>();
        var driver = Substitute.For<IAgentDriver>();
        driver.AgentType.Returns("codex");
        var activation = Substitute.For<IOptionsMonitor<ActivationOptions>>();
        activation.CurrentValue.Returns(new ActivationOptions
        {
            Enabled = true,
            WorkingDirectory = _root,
            ProviderPriority = ["codex"],
            MaxConcurrentRuns = new Dictionary<string, int> { ["codex"] = 1 },
        });
        var itogurumaOptions = Options.Create(new ItogurumaClientOptions { ReceiveBatchSize = 50, LeaseSeconds = 300 });
        var worker = new ItogurumaConnectionWorker(
            client, queue, new AgentProviderSelector([driver]), new ItogurumaConnectionState(),
            itogurumaOptions, activation, NullLogger<ItogurumaConnectionWorker>.Instance);

        await worker.PollProjectsAsync(CancellationToken.None);

        await client.Received(1).RegisterProjectAsync("crs", Arg.Any<CancellationToken>());
        await client.Received(1).RegisterProjectAsync("hataori", Arg.Any<CancellationToken>());
        await client.Received(1).GetMessagesAsync("crs", 50, 300, Arg.Is<string?>(value => value == null), Arg.Any<CancellationToken>());
        await client.Received(1).GetMessagesAsync("hataori", 50, 300, Arg.Is<string?>(value => value == null), Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
        GC.SuppressFinalize(this);
    }
}
