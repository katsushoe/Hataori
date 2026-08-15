using Hataori.Core.Tasks;

namespace Hataori.Application.Tasks;

/// <summary>
/// Taskライフサイクルのユースケースを提供します。
/// </summary>
public sealed class TaskService
{
    private readonly ITaskRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TaskService(ITaskRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<HataoriTask> StartAsync(
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        string summary,
        string currentWork,
        CancellationToken cancellationToken)
    {
        var task = HataoriTask.Start(taskId, taskName, agentId, conversationId, originMessageId, summary, currentWork, _timeProvider.GetUtcNow());
        await _repository.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<HataoriTask> HeartbeatAsync(string taskId, string currentWork, int progressPercent, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.Heartbeat(currentWork, progressPercent, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, "heartbeat", cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<HataoriTask> CompleteAsync(string taskId, string result, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.Complete(result, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, "completed", cancellationToken).ConfigureAwait(false);
        return task;
    }

    public Task<IReadOnlyList<HataoriTask>> ListAsync(HataoriTaskStatus? status, string? agentId, CancellationToken cancellationToken)
    {
        return _repository.ListAsync(status, agentId, cancellationToken);
    }

    public Task<HataoriTask?> GetAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetAsync(taskId, cancellationToken);
    }

    public Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetHistoryAsync(taskId, cancellationToken);
    }

    public Task<IReadOnlyList<TaskRelation>> GetRelationsAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetRelationsAsync(taskId, cancellationToken);
    }

    public async Task AddRelationAsync(string taskId, string relatedTaskId, string relationType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relatedTaskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationType);
        await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        await GetRequiredAsync(relatedTaskId, cancellationToken).ConfigureAwait(false);
        await _repository.AddRelationAsync(new TaskRelation(taskId, relatedTaskId, relationType), cancellationToken).ConfigureAwait(false);
    }

    public Task<HataoriTask> CancelAsync(string taskId, string? result, CancellationToken cancellationToken)
    {
        return EndAsync(taskId, HataoriTaskStatus.Cancelled, "cancelled", result, cancellationToken);
    }

    public Task<HataoriTask> FailAsync(string taskId, string result, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        return EndAsync(taskId, HataoriTaskStatus.Failed, "failed", result, cancellationToken);
    }

    public Task<HataoriTask> ExpireAsync(string taskId, CancellationToken cancellationToken)
    {
        return EndAsync(taskId, HataoriTaskStatus.Expired, "expired", null, cancellationToken);
    }

    private async Task<HataoriTask> EndAsync(string taskId, HataoriTaskStatus status, string eventType, string? result, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.End(status, result, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, eventType, cancellationToken).ConfigureAwait(false);
        return task;
    }

    private async Task<HataoriTask> GetRequiredAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return await _repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
    }
}
