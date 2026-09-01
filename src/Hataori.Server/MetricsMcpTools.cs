using System.ComponentModel;
using Hataori.Application.Metrics;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>AI Agentへ公開する運用Metricsツールです。</summary>
[McpServerToolType]
public sealed class MetricsMcpTools(OperationalMetricsService service)
{
    /// <summary>Workspace単位の運用Metricsを返します。</summary>
    [McpServerTool(Name = "metrics_get", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns workspace-scoped task, message, retry, agent-run, duration, success-rate, and per-agent operational metrics from persisted SQLite data.")]
    public Task<OperationalMetrics> GetAsync(string workspaceId, CancellationToken cancellationToken) => service.GetAsync(workspaceId, cancellationToken);
}
