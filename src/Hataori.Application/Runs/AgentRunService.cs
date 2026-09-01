using Hataori.Core.Runs;
using Hataori.Core.Workspaces;

namespace Hataori.Application.Runs;

public sealed class AgentRunService(IAgentRunRepository repository, TimeProvider timeProvider)
{
    public async Task<AgentRun> QueueAsync(string runId, string messageId, string conversationId, string agentId, CancellationToken cancellationToken)
        => await QueueAsync(WorkspaceId.Default, runId, messageId, conversationId, agentId, cancellationToken).ConfigureAwait(false);

    public async Task<AgentRun> QueueAsync(string workspaceId, string runId, string messageId, string conversationId, string agentId, CancellationToken cancellationToken)
    {
        var run = AgentRun.Queue(workspaceId, runId, messageId, conversationId, agentId, timeProvider.GetUtcNow());
        await repository.AddAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public Task<AgentRun?> GetAsync(string runId, CancellationToken cancellationToken) => repository.GetAsync(runId, cancellationToken);

    public Task<IReadOnlyList<AgentRun>> ListAsync(AgentRunStatus? status, string? agentId, CancellationToken cancellationToken) =>
        repository.ListAsync(status, agentId, cancellationToken);

    public async Task<IReadOnlyList<AgentRun>> ListAsync(string workspaceId, AgentRunStatus? status, string? agentId, CancellationToken cancellationToken) =>
        (await repository.ListAsync(status, agentId, cancellationToken).ConfigureAwait(false))
            .Where(run => run.WorkspaceId == WorkspaceId.Normalize(workspaceId)).ToArray();

    public async Task<AgentRun> MarkStartingAsync(string runId, CancellationToken cancellationToken)
    {
        var run = await GetRequiredAsync(runId, cancellationToken).ConfigureAwait(false);
        run.MarkStarting();
        await repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<AgentRun> MarkRunningAsync(string runId, int processId, CancellationToken cancellationToken)
    {
        var run = await GetRequiredAsync(runId, cancellationToken).ConfigureAwait(false);
        run.MarkRunning(processId, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<AgentRun> CompleteAsync(string runId, string nativeSessionId, int exitCode, string? finalMessage, CancellationToken cancellationToken)
    {
        var run = await GetRequiredAsync(runId, cancellationToken).ConfigureAwait(false);
        run.Complete(nativeSessionId, exitCode, finalMessage, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<AgentRun> FailAsync(string runId, int? exitCode, string error, CancellationToken cancellationToken)
    {
        var run = await GetRequiredAsync(runId, cancellationToken).ConfigureAwait(false);
        run.Fail(exitCode, error, timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<AgentRun> CancelAsync(string runId, CancellationToken cancellationToken)
    {
        var run = await GetRequiredAsync(runId, cancellationToken).ConfigureAwait(false);
        run.Cancel(timeProvider.GetUtcNow());
        await repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }

    private async Task<AgentRun> GetRequiredAsync(string runId, CancellationToken cancellationToken) =>
        await repository.GetAsync(runId, cancellationToken).ConfigureAwait(false)
        ?? throw new KeyNotFoundException($"Agent run '{runId}' was not found.");
}
