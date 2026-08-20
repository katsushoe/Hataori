using System.ComponentModel;
using Hataori.Application.Activation;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>
/// AI Agentへ公開するAgent Run操作ツールです。
/// </summary>
[McpServerToolType]
public sealed class AgentRunMcpTools
{
    private readonly ActivationManager _activation;

    public AgentRunMcpTools(ActivationManager activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        _activation = activation;
    }

    [McpServerTool(Name = "agent_run_cancel", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Cancels a queued, starting, or running agent run. Terminates the underlying process if it is currently live.")]
    public Task<bool> CancelAsync(string runId, CancellationToken cancellationToken)
        => _activation.RequestRunCancellationAsync(runId, cancellationToken);
}
