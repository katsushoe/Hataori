using Hataori.Core.Runs;

namespace Hataori.Application.Runs;

public interface IAgentRunRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task AddAsync(AgentRun run, CancellationToken cancellationToken);
    Task SaveAsync(AgentRun run, CancellationToken cancellationToken);
    Task<AgentRun?> GetAsync(string runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRun>> ListAsync(AgentRunStatus? status, string? agentId, CancellationToken cancellationToken);
}
