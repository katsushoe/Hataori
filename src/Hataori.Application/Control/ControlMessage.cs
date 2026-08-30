namespace Hataori.Application.Control;

/// <summary>
/// ローカルControl Pipeの入力です。
/// </summary>
public sealed record ControlRequest(string Command, string? Argument = null);

/// <summary>
/// ローカルControl Pipeの応答です。
/// </summary>
public sealed record ControlResponse(bool Success, string Status, DateTimeOffset TimestampUtc, MonitorSnapshot? Monitor = null);

/// <summary>Monitorへ返す読み取り専用の状態スナップショットです。</summary>
public sealed record MonitorSnapshot(
    IReadOnlyList<MonitorTask> Tasks,
    IReadOnlyList<MonitorAgentStatus> Agents,
    IReadOnlyList<MonitorSession> Sessions,
    IReadOnlyList<MonitorRun> Runs,
    int QueueCount,
    MonitorSystemStatus System);

/// <summary>Monitorへ返すタスク表示情報です。</summary>
public sealed record MonitorTask(
    string WorkspaceId,
    string TaskId,
    string TaskName,
    string AgentId,
    string? ConversationId,
    string Status,
    string CurrentWork,
    int ProgressPercent,
    DateTimeOffset LastActivityAtUtc);

/// <summary>Monitorへ返す会話セッション表示情報です。</summary>
public sealed record MonitorSession(
    string ConversationId,
    string AgentId,
    string NativeSessionId,
    string Status,
    DateTimeOffset LastUsedAtUtc);

/// <summary>Monitorへ返すAgent実行表示情報です。</summary>
public sealed record MonitorRun(
    string RunId,
    string MessageId,
    string ConversationId,
    string AgentId,
    string Status,
    DateTimeOffset QueuedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Error);

/// <summary>Agent単位の実行状態です。</summary>
public sealed record MonitorAgentStatus(string AgentId, string? ConversationId, string State, int ActiveRuns);

/// <summary>Monitorに表示する基盤状態です。</summary>
public sealed record MonitorSystemStatus(string Server, string Itoguruma, string Mcp, string Sqlite);
