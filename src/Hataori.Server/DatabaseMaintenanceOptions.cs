namespace Hataori.Server;

/// <summary>定期Database Maintenance設定です。</summary>
public sealed class DatabaseMaintenanceOptions
{
    public const string SectionName = "databaseMaintenance";
    public bool Enabled { get; init; } = true;
    public int IntervalHours { get; init; } = 24;
    public int StaleTaskHours { get; init; } = 24;
    public int TaskRetentionDays { get; init; } = 90;
    public int AgentRunRetentionDays { get; init; } = 30;
    public int MessageRetentionDays { get; init; } = 30;
    public bool Vacuum { get; init; } = true;
}
