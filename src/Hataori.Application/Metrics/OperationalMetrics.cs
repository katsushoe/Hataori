namespace Hataori.Application.Metrics;

/// <summary>Workspace単位の運用Metrics snapshotです。</summary>
public sealed record OperationalMetrics(
    string WorkspaceId,
    DateTimeOffset GeneratedAtUtc,
    TaskMetrics Tasks,
    MessageMetrics Messages,
    AgentRunMetrics AgentRuns,
    IReadOnlyList<AgentMetrics> Agents);

/// <summary>Task Metricsです。</summary>
public sealed record TaskMetrics(int Total, int Active, int Completed, int Failed, int Cancelled, int Expired, double CompletionRatePercent, double AverageDurationSeconds);

/// <summary>Message Metricsです。</summary>
public sealed record MessageMetrics(int Total, int Queued, int Starting, int Running, int Responded, int Failed, int Cancelled, int PendingReplyRetries, int ReplyAttempts, double ResponseRatePercent, double AverageProcessingSeconds);

/// <summary>Agent Run Metricsです。</summary>
public sealed record AgentRunMetrics(int Total, int Queued, int Starting, int Running, int Completed, int Failed, int Cancelled, double SuccessRatePercent, double AverageQueueWaitSeconds, double AverageRunDurationSeconds);

/// <summary>Agent別Run Metricsです。</summary>
public sealed record AgentMetrics(string AgentId, int TotalRuns, int CompletedRuns, int FailedRuns, int CancelledRuns, double SuccessRatePercent, double AverageRunDurationSeconds);
