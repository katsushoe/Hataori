namespace Hataori.Application.Metrics;

/// <summary>運用Metricsを取得するRepositoryです。</summary>
public interface IOperationalMetricsRepository
{
    /// <summary>指定WorkspaceのMetrics snapshotを取得します。</summary>
    Task<OperationalMetrics> GetAsync(string workspaceId, CancellationToken cancellationToken);
}
