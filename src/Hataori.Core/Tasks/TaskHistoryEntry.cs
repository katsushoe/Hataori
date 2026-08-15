namespace Hataori.Core.Tasks;

/// <summary>
/// Taskの状態変更履歴を表します。
/// </summary>
public sealed record TaskHistoryEntry(
    long HistoryId,
    string TaskId,
    DateTimeOffset CreatedAtUtc,
    string EventType,
    string Message,
    int ProgressPercent);
