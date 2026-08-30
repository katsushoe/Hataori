namespace Hataori.Core.Tasks;

using WorkspaceIdentifier = Hataori.Core.Workspaces.WorkspaceId;

/// <summary>
/// AI エージェントが実行する作業単位を表します。
/// </summary>
public sealed class HataoriTask
{
    private HataoriTask(
        string workspaceId,
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        string summary,
        string currentWork,
        DateTimeOffset startedAtUtc)
    {
        WorkspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        TaskId = taskId;
        TaskName = taskName;
        AgentId = agentId;
        ConversationId = conversationId;
        OriginMessageId = originMessageId;
        Status = HataoriTaskStatus.Active;
        Summary = summary;
        CurrentWork = currentWork;
        StartedAtUtc = startedAtUtc;
        LastActivityAtUtc = startedAtUtc;
    }

    public string WorkspaceId { get; }
    public string TaskId { get; }
    public string TaskName { get; }
    public string AgentId { get; }
    public string? ConversationId { get; }
    public string? OriginMessageId { get; }
    public HataoriTaskStatus Status { get; private set; }
    public string Summary { get; private set; }
    public string CurrentWork { get; private set; }
    public int ProgressPercent { get; private set; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? Result { get; private set; }

    /// <summary>
    /// 新しい active タスクを作成します。
    /// </summary>
    public static HataoriTask Start(
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        string summary,
        string currentWork,
        DateTimeOffset startedAtUtc)
        => Start(WorkspaceIdentifier.Default, taskId, taskName, agentId, conversationId, originMessageId, summary, currentWork, startedAtUtc);

    /// <summary>指定Workspaceで新しいactive Taskを作成します。</summary>
    public static HataoriTask Start(
        string workspaceId,
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        string summary,
        string currentWork,
        DateTimeOffset startedAtUtc)
    {
        workspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(currentWork);

        return new HataoriTask(
            workspaceId,
            taskId,
            taskName,
            agentId,
            conversationId,
            originMessageId,
            summary,
            currentWork,
            startedAtUtc.ToUniversalTime());
    }

    /// <summary>
    /// 永続化された値からタスクを復元します。
    /// </summary>
    public static HataoriTask Restore(
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        HataoriTaskStatus status,
        string summary,
        string currentWork,
        int progressPercent,
        DateTimeOffset startedAtUtc,
        DateTimeOffset lastActivityAtUtc,
        DateTimeOffset? completedAtUtc,
        string? result)
        => Restore(WorkspaceIdentifier.Default, taskId, taskName, agentId, conversationId, originMessageId, status, summary, currentWork, progressPercent, startedAtUtc, lastActivityAtUtc, completedAtUtc, result);

    /// <summary>指定Workspaceの永続化された値からTaskを復元します。</summary>
    public static HataoriTask Restore(
        string workspaceId,
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        HataoriTaskStatus status,
        string summary,
        string currentWork,
        int progressPercent,
        DateTimeOffset startedAtUtc,
        DateTimeOffset lastActivityAtUtc,
        DateTimeOffset? completedAtUtc,
        string? result)
    {
        var task = Start(workspaceId, taskId, taskName, agentId, conversationId, originMessageId, summary, currentWork, startedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(progressPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(progressPercent, 100);
        task.Status = status;
        task.ProgressPercent = progressPercent;
        task.LastActivityAtUtc = lastActivityAtUtc.ToUniversalTime();
        task.CompletedAtUtc = completedAtUtc?.ToUniversalTime();
        task.Result = result;
        return task;
    }

    /// <summary>
    /// active タスクの進捗と最終活動日時を更新します。
    /// </summary>
    public void Heartbeat(string currentWork, int progressPercent, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(currentWork);
        ArgumentOutOfRangeException.ThrowIfLessThan(progressPercent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(progressPercent, 100);

        CurrentWork = currentWork;
        ProgressPercent = progressPercent;
        LastActivityAtUtc = occurredAtUtc.ToUniversalTime();
    }

    /// <summary>
    /// active タスクを完了します。
    /// </summary>
    public void Complete(string result, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(result);

        var completedAtUtc = occurredAtUtc.ToUniversalTime();
        Status = HataoriTaskStatus.Completed;
        ProgressPercent = 100;
        Result = result;
        CompletedAtUtc = completedAtUtc;
        LastActivityAtUtc = completedAtUtc;
    }

    /// <summary>
    /// active タスクを指定状態で終了します。
    /// </summary>
    public void End(HataoriTaskStatus status, string? result, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (status is HataoriTaskStatus.Active or HataoriTaskStatus.Completed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        var endedAtUtc = occurredAtUtc.ToUniversalTime();
        Status = status;
        Result = result;
        CompletedAtUtc = endedAtUtc;
        LastActivityAtUtc = endedAtUtc;
    }

    private void EnsureActive()
    {
        if (Status != HataoriTaskStatus.Active)
        {
            throw new InvalidOperationException("Only active tasks can be updated.");
        }
    }
}
