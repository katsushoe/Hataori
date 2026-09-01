namespace Hataori.Application.Agents;

/// <summary>Workspaceに登録されたAgent定義です。</summary>
public sealed record AgentDefinition(
    string WorkspaceId,
    string AgentId,
    bool Enabled,
    int MaxConcurrentRuns,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Agent定義の監査履歴です。</summary>
public sealed record AgentDefinitionHistory(
    long HistoryId,
    string WorkspaceId,
    string AgentId,
    bool Enabled,
    int MaxConcurrentRuns,
    string EventType,
    DateTimeOffset CreatedAtUtc);
