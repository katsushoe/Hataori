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

    private async Task<HataoriTask> GetRequiredAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return await _repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
    }
}
