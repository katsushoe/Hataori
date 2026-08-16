namespace Hataori.Infrastructure.Maintenance;

/// <summary>SQLite Maintenanceの保持期間設定です。</summary>
public sealed record DatabaseMaintenanceSettings(
    TimeSpan StaleTaskAge,
    TimeSpan TaskRetention,
    TimeSpan AgentRunRetention,
    TimeSpan MessageRetention,
    bool Vacuum);

/// <summary>Maintenanceで変更した行数です。</summary>
public sealed record DatabaseMaintenanceResult(int ExpiredTasks, int PurgedTasks, int PurgedAgentRuns, int PurgedMessages, bool Vacuumed);
