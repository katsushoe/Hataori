namespace Hataori.Core.Tasks;

/// <summary>
/// Hataori が永続化するタスクの状態です。
/// </summary>
public enum HataoriTaskStatus
{
    Active,
    Completed,
    Cancelled,
    Failed,
    Expired,
}
