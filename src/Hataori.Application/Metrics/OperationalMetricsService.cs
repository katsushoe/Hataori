using Hataori.Core.Workspaces;

namespace Hataori.Application.Metrics;

/// <summary>運用Metricsの参照ユースケースです。</summary>
public sealed class OperationalMetricsService(IOperationalMetricsRepository repository)
{
    /// <summary>指定WorkspaceのMetrics snapshotを取得します。</summary>
    public Task<OperationalMetrics> GetAsync(string workspaceId, CancellationToken cancellationToken) =>
        repository.GetAsync(WorkspaceId.Normalize(workspaceId), cancellationToken);
}
