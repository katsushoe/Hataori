using FluentAssertions;
using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hataori.Infrastructure.Tests.Itoguruma;

/// <summary>
/// ローカルで稼働中のItogurumaに対し、専用の検証Projectで送信・リース・ACKを確認します。
/// 環境変数<c>HATAORI_ITOGURUMA_LIVE_TEST=1</c>を指定した場合だけ実行します。
/// 実行後は検証Project<c>hataori_lease_test</c>と履歴が残るため、Itogurumaで<c>delete_agent_history</c>（agent_id=hataori_lease_test）、
/// <c>unregister_agent</c>、<c>delete_project</c>の順に実行して削除してください。
/// </summary>
public sealed class ItogurumaLiveLeaseTests
{
    private const string ProjectId = "hataori_lease_test";
    private static readonly Uri Endpoint = new("http://127.0.0.1:47631/mcp");

    [LiveItogurumaFact]
    public async Task LeaseAndAcknowledge_AgainstLocalItoguruma_RemovesMessageFromInbox()
    {
        var token = ItogurumaWindowsCredential.Read(Endpoint)
            ?? throw new InvalidOperationException("The current user's Itoguruma credential was not found.");
        var options = new ItogurumaClientOptions
        {
            Endpoint = Endpoint,
            AuthenticationToken = token,
            AgentId = ProjectId,
            AgentType = "hataori",
            ProjectId = ProjectId,
            ConnectionTimeoutSeconds = 10,
        };
        await using var client = new McpItogurumaClient(options, NullLoggerFactory.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var thread = $"hataori-lease-test-{Guid.NewGuid():N}";

        await client.ConnectAsync(timeout.Token);
        await client.ReplyAsync(ProjectId, "codex", "Hataori lease verification", thread, null, thread, timeout.Token);
        var leased = await client.GetMessagesAsync(ProjectId, 10, 60, thread, timeout.Token);

        leased.Should().ContainSingle();
        var message = leased[0];
        message.LeaseId.Should().NotBeNullOrWhiteSpace();
        (await client.AcknowledgeAsync(ProjectId, message.MessageId, message.LeaseId!, timeout.Token)).Should().BeTrue();
        (await client.GetMessagesAsync(ProjectId, 10, 60, thread, timeout.Token)).Should().BeEmpty();
    }
}

/// <summary>ローカルItogurumaへの実機テストを、明示的に有効化した場合だけ実行します。</summary>
public sealed class LiveItogurumaFactAttribute : FactAttribute
{
    public LiveItogurumaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HATAORI_ITOGURUMA_LIVE_TEST") != "1")
        {
            Skip = "Set HATAORI_ITOGURUMA_LIVE_TEST=1 to run against the local Itoguruma.";
        }
    }
}
