namespace Hataori.Application.Agents;

/// <summary>Agent定義を永続化します。</summary>
public interface IAgentDefinitionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDefinition>> ListAsync(string? workspaceId, CancellationToken cancellationToken);
    Task<AgentDefinition?> GetAsync(string workspaceId, string agentId, CancellationToken cancellationToken);
    Task UpsertAsync(AgentDefinition definition, string eventType, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentDefinitionHistory>> GetHistoryAsync(string workspaceId, string agentId, CancellationToken cancellationToken);
}
