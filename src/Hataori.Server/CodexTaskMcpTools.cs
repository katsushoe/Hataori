using System.ComponentModel;
using Hataori.Application.Codex;
using Hataori.Core.Codex;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>Codex Desktop受信タスクへタスク起動要求を公開します。</summary>
[McpServerToolType]
public sealed class CodexTaskMcpTools(CodexTaskLaunchService service)
{
    [McpServerTool(Name = "codex_task_claim", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Claims the next Codex Desktop task launch request. Returns null when no request is pending.")]
    public Task<CodexTaskLaunch?> ClaimAsync(int leaseSeconds = 300, CancellationToken cancellationToken = default)
        => service.ClaimAsync(leaseSeconds, cancellationToken);

    [McpServerTool(Name = "codex_task_started", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records the Codex Desktop task ID after create_thread succeeds.")]
    public async Task<object> MarkStartedAsync(string messageId, string claimToken, string codexTaskId, CancellationToken cancellationToken)
    {
        await service.MarkStartedAsync(messageId, claimToken, codexTaskId, cancellationToken).ConfigureAwait(false);
        return new { messageId, codexTaskId, status = "started" };
    }

    [McpServerTool(Name = "codex_task_release", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Releases a claimed launch request after task creation fails so it can be retried.")]
    public async Task<object> ReleaseAsync(string messageId, string claimToken, string error, CancellationToken cancellationToken)
    {
        await service.ReleaseAsync(messageId, claimToken, error, cancellationToken).ConfigureAwait(false);
        return new { messageId, status = "pending" };
    }
}
