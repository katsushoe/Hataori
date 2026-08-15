using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Core.Tasks;

namespace Hataori.Application.Control;

/// <summary>
/// ローカルControl Pipeの入力です。
/// </summary>
public sealed record ControlRequest(string Command);

/// <summary>
/// ローカルControl Pipeの応答です。
/// </summary>
public sealed record ControlResponse(bool Success, string Status, DateTimeOffset TimestampUtc, MonitorSnapshot? Monitor = null);

/// <summary>Monitorへ返す読み取り専用の状態スナップショットです。</summary>
public sealed record MonitorSnapshot(
    IReadOnlyList<HataoriTask> Tasks,
    IReadOnlyList<MonitorAgentStatus> Agents,
    IReadOnlyList<ConversationSession> Sessions,
    IReadOnlyList<AgentRun> Runs,
    int QueueCount,
    MonitorSystemStatus System);

/// <summary>Agent単位の実行状態です。</summary>
public sealed record MonitorAgentStatus(string AgentId, string? ConversationId, string State, int ActiveRuns);

/// <summary>Monitorに表示する基盤状態です。</summary>
public sealed record MonitorSystemStatus(string Server, string Itoguruma, string Mcp, string Sqlite);
