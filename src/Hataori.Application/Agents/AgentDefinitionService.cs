using Hataori.Core.Workspaces;

namespace Hataori.Application.Agents;

/// <summary>Agent定義の管理ユースケースです。</summary>
public sealed class AgentDefinitionService(IAgentDefinitionRepository repository, TimeProvider timeProvider)
{
    /// <summary>Agent定義を一覧します。</summary>
    public Task<IReadOnlyList<AgentDefinition>> ListAsync(string? workspaceId, CancellationToken cancellationToken) =>
        repository.ListAsync(workspaceId is null ? null : WorkspaceId.Normalize(workspaceId), cancellationToken);

    /// <summary>Agent定義を登録または更新します。</summary>
    public async Task<AgentDefinition> SetAsync(string workspaceId, string agentId, bool enabled, int maxConcurrentRuns, CancellationToken cancellationToken)
    {
        workspaceId = WorkspaceId.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        if (maxConcurrentRuns is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRuns), "Maximum concurrent runs must be between 0 and 64.");
        }

        agentId = agentId.Trim().ToLowerInvariant();
        var existing = await repository.GetAsync(workspaceId, agentId, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var definition = new AgentDefinition(workspaceId, agentId, enabled, maxConcurrentRuns, existing?.CreatedAtUtc ?? now, now);
        await repository.UpsertAsync(definition, existing is null ? "created" : "updated", cancellationToken).ConfigureAwait(false);
        return definition;
    }

    /// <summary>Agent定義の監査履歴を取得します。</summary>
    public Task<IReadOnlyList<AgentDefinitionHistory>> HistoryAsync(string workspaceId, string agentId, CancellationToken cancellationToken)
    {
        workspaceId = WorkspaceId.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return repository.GetHistoryAsync(workspaceId, agentId.Trim().ToLowerInvariant(), cancellationToken);
    }
}
