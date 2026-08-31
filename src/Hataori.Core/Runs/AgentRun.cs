namespace Hataori.Core.Runs;

using WorkspaceIdentifier = Hataori.Core.Workspaces.WorkspaceId;

public sealed class AgentRun
{
    private AgentRun(string workspaceId, string runId, string messageId, string conversationId, string agentId, DateTimeOffset queuedAtUtc)
    {
        WorkspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        RunId = runId;
        MessageId = messageId;
        ConversationId = conversationId;
        AgentId = agentId;
        Status = AgentRunStatus.Queued;
        QueuedAtUtc = queuedAtUtc.ToUniversalTime();
    }

    public string WorkspaceId { get; }
    public string RunId { get; }
    public string MessageId { get; }
    public string ConversationId { get; }
    public string AgentId { get; }
    public string? NativeSessionId { get; private set; }
    public int? ProcessId { get; private set; }
    public AgentRunStatus Status { get; private set; }
    public DateTimeOffset QueuedAtUtc { get; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public int? ExitCode { get; private set; }
    public string? FinalMessage { get; private set; }
    public string? Error { get; private set; }

    public static AgentRun Queue(string runId, string messageId, string conversationId, string agentId, DateTimeOffset queuedAtUtc)
        => Queue(WorkspaceIdentifier.Default, runId, messageId, conversationId, agentId, queuedAtUtc);

    public static AgentRun Queue(string workspaceId, string runId, string messageId, string conversationId, string agentId, DateTimeOffset queuedAtUtc)
    {
        workspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return new AgentRun(workspaceId, runId, messageId, conversationId, agentId, queuedAtUtc);
    }

    public static AgentRun Restore(
        string runId, string messageId, string conversationId, string agentId, string? nativeSessionId,
        int? processId, AgentRunStatus status, DateTimeOffset queuedAtUtc, DateTimeOffset? startedAtUtc,
        DateTimeOffset? endedAtUtc, int? exitCode, string? finalMessage, string? error)
        => Restore(WorkspaceIdentifier.Default, runId, messageId, conversationId, agentId, nativeSessionId, processId, status, queuedAtUtc, startedAtUtc, endedAtUtc, exitCode, finalMessage, error);

    public static AgentRun Restore(
        string workspaceId, string runId, string messageId, string conversationId, string agentId, string? nativeSessionId,
        int? processId, AgentRunStatus status, DateTimeOffset queuedAtUtc, DateTimeOffset? startedAtUtc,
        DateTimeOffset? endedAtUtc, int? exitCode, string? finalMessage, string? error)
    {
        var run = Queue(workspaceId, runId, messageId, conversationId, agentId, queuedAtUtc);
        run.NativeSessionId = nativeSessionId;
        run.ProcessId = processId;
        run.Status = status;
        run.StartedAtUtc = startedAtUtc?.ToUniversalTime();
        run.EndedAtUtc = endedAtUtc?.ToUniversalTime();
        run.ExitCode = exitCode;
        run.FinalMessage = finalMessage;
        run.Error = error;
        return run;
    }

    public void MarkStarting()
    {
        EnsureStatus(AgentRunStatus.Queued);
        Status = AgentRunStatus.Starting;
    }

    public void MarkRunning(int processId, DateTimeOffset startedAtUtc)
    {
        EnsureStatus(AgentRunStatus.Starting);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ProcessId = processId;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        Status = AgentRunStatus.Running;
    }

    public void Complete(string nativeSessionId, int exitCode, string? finalMessage, DateTimeOffset endedAtUtc)
    {
        EnsureStatus(AgentRunStatus.Running);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        if (exitCode != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exitCode), "A completed run must have exit code zero.");
        }

        NativeSessionId = nativeSessionId;
        ExitCode = exitCode;
        FinalMessage = finalMessage;
        End(AgentRunStatus.Completed, endedAtUtc);
    }

    public void Fail(int? exitCode, string error, DateTimeOffset endedAtUtc)
    {
        EnsureActive();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ExitCode = exitCode;
        Error = error;
        End(AgentRunStatus.Failed, endedAtUtc);
    }

    public void Cancel(DateTimeOffset endedAtUtc)
    {
        if (Status is not (AgentRunStatus.Queued or AgentRunStatus.Starting or AgentRunStatus.Running))
        {
            throw new InvalidOperationException("Only queued, starting, or running runs can be cancelled.");
        }

        End(AgentRunStatus.Cancelled, endedAtUtc);
    }

    private void End(AgentRunStatus status, DateTimeOffset endedAtUtc)
    {
        Status = status;
        EndedAtUtc = endedAtUtc.ToUniversalTime();
    }

    private void EnsureStatus(AgentRunStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Run must be {expected.ToString().ToLowerInvariant()}.");
        }
    }

    private void EnsureActive()
    {
        if (Status is not (AgentRunStatus.Starting or AgentRunStatus.Running))
        {
            throw new InvalidOperationException("Only starting or running runs can end.");
        }
    }
}
