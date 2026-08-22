using FluentAssertions;
using Hataori.Application.Codex;
using Hataori.Infrastructure.Codex;
using Hataori.Infrastructure.Messages;
using Hataori.Core.Messages;
using Microsoft.Data.Sqlite;

namespace Hataori.Server.Tests;

public sealed class CodexTaskMcpToolsTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-codex-mcp-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ClaimAndStarted_UseSharedLaunchService()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true, Pooling = false }.ToString();
        var queue = new SqliteMessageQueueRepository(connectionString);
        await queue.InitializeAsync(CancellationToken.None);
        await queue.EnqueueAsync(new IncomingMessage("msg-1", "thread-1", "codex", Path.Combine("F:\\Workspace\\Projects", "CRs"), "sender", null, "message", "prompt", null, DateTimeOffset.UtcNow), 0, CancellationToken.None);
        var tools = new CodexTaskMcpTools(new CodexTaskLaunchService(new SqliteCodexTaskLaunchRepository(connectionString), TimeProvider.System));

        var claimed = await tools.ClaimAsync(300, CancellationToken.None);
        var result = await tools.MarkStartedAsync(claimed!.MessageId, claimed.ClaimToken, "codex-thread", CancellationToken.None);

        result.Should().BeEquivalentTo(new { messageId = "msg-1", codexTaskId = "codex-thread", status = "started" });
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
