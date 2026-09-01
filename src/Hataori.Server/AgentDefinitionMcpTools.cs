using System.ComponentModel;
using Hataori.Application.Agents;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>AI Agentへ公開するAgent定義管理ツールです。</summary>
[McpServerToolType]
public sealed class AgentDefinitionMcpTools(AgentDefinitionService service)
{
    [McpServerTool(Name = "agent_definition_list", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists persisted agent definitions, optionally filtered by workspace ID.")]
    public Task<IReadOnlyList<AgentDefinition>> ListAsync(string? workspaceId, CancellationToken cancellationToken) =>
        service.ListAsync(workspaceId, cancellationToken);

    [McpServerTool(Name = "agent_definition_set", OpenWorld = false, UseStructuredContent = true)]
    [Description("Creates or updates a workspace-scoped agent definition and records its audit history.")]
    public Task<AgentDefinition> SetAsync(string workspaceId, string agentId, bool enabled, int maxConcurrentRuns, CancellationToken cancellationToken) =>
        service.SetAsync(workspaceId, agentId, enabled, maxConcurrentRuns, cancellationToken);

    [McpServerTool(Name = "agent_definition_history", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists the audit history for a workspace-scoped agent definition.")]
    public Task<IReadOnlyList<AgentDefinitionHistory>> HistoryAsync(string workspaceId, string agentId, CancellationToken cancellationToken) =>
        service.HistoryAsync(workspaceId, agentId, cancellationToken);
}
