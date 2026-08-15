using System.ComponentModel;
using Hataori.Application.Tasks;
using Hataori.Core.Tasks;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>
/// AI Agentへ公開するTask操作ツールです。
/// </summary>
[McpServerToolType]
public sealed class TaskMcpTools
{
    private readonly TaskService _service;

    public TaskMcpTools(TaskService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    [McpServerTool(Name = "task_start", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts a task before an AI agent begins implementation work.")]
    public Task<HataoriTask> StartAsync(string taskId, string taskName, string agentId, string? conversationId, string? originMessageId, string summary, string currentWork, CancellationToken cancellationToken)
        => _service.StartAsync(taskId, taskName, agentId, conversationId, originMessageId, summary, currentWork, cancellationToken);

    [McpServerTool(Name = "task_get", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets a task by its unique identifier.")]
    public async Task<HataoriTask> GetAsync(string taskId, CancellationToken cancellationToken)
        => await _service.GetAsync(taskId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");

    [McpServerTool(Name = "task_list", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists tasks, optionally filtered by status and agent.")]
    public Task<IReadOnlyList<HataoriTask>> ListAsync(HataoriTaskStatus? status, string? agentId, CancellationToken cancellationToken)
        => _service.ListAsync(status, agentId, cancellationToken);

    [McpServerTool(Name = "task_heartbeat", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Updates the current work and progress percentage of an active task.")]
    public Task<HataoriTask> HeartbeatAsync(string taskId, string currentWork, int progressPercent, CancellationToken cancellationToken)
        => _service.HeartbeatAsync(taskId, currentWork, progressPercent, cancellationToken);

    [McpServerTool(Name = "task_complete", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Marks an active task as completed.")]
    public Task<HataoriTask> CompleteAsync(string taskId, string result, CancellationToken cancellationToken)
        => _service.CompleteAsync(taskId, result, cancellationToken);

    [McpServerTool(Name = "task_cancel", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Cancels an active task.")]
    public Task<HataoriTask> CancelAsync(string taskId, string? result, CancellationToken cancellationToken)
        => _service.CancelAsync(taskId, result, cancellationToken);

    [McpServerTool(Name = "task_fail", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Marks an active task as failed.")]
    public Task<HataoriTask> FailAsync(string taskId, string result, CancellationToken cancellationToken)
        => _service.FailAsync(taskId, result, cancellationToken);

    [McpServerTool(Name = "task_expire", Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Marks an inactive task as expired.")]
    public Task<HataoriTask> ExpireAsync(string taskId, CancellationToken cancellationToken)
        => _service.ExpireAsync(taskId, cancellationToken);

    [McpServerTool(Name = "task_history", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the ordered history of a task.")]
    public Task<IReadOnlyList<TaskHistoryEntry>> HistoryAsync(string taskId, CancellationToken cancellationToken)
        => _service.GetHistoryAsync(taskId, cancellationToken);

    [McpServerTool(Name = "task_relations", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets relations involving a task.")]
    public Task<IReadOnlyList<TaskRelation>> RelationsAsync(string taskId, CancellationToken cancellationToken)
        => _service.GetRelationsAsync(taskId, cancellationToken);

    [McpServerTool(Name = "task_relation_add", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Adds an idempotent relation between two existing tasks.")]
    public Task AddRelationAsync(string taskId, string relatedTaskId, string relationType, CancellationToken cancellationToken)
        => _service.AddRelationAsync(taskId, relatedTaskId, relationType, cancellationToken);
}
