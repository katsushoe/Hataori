using Hataori.Core.Tasks;

namespace Hataori.Application.Tasks;

/// <summary>
/// Task集約の永続化境界です。
/// </summary>
public interface ITaskRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task AddAsync(HataoriTask task, CancellationToken cancellationToken);
    Task<HataoriTask?> GetAsync(string taskId, CancellationToken cancellationToken);
    Task<IReadOnlyList<HataoriTask>> ListAsync(HataoriTaskStatus? status, string? agentId, CancellationToken cancellationToken);
    Task UpdateAsync(HataoriTask task, string eventType, CancellationToken cancellationToken);
    Task UpdateAsync(HataoriTask task, string eventType, string? message, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(string taskId, CancellationToken cancellationToken);
    Task AddRelationAsync(TaskRelation relation, CancellationToken cancellationToken);
    Task<IReadOnlyList<TaskRelation>> GetRelationsAsync(string taskId, CancellationToken cancellationToken);
}
