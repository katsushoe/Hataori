namespace Hataori.Core.Sessions;

using WorkspaceIdentifier = Hataori.Core.Workspaces.WorkspaceId;

public sealed class ConversationSession
{
    private ConversationSession(string workspaceId, string conversationId, string agentId, string nativeSessionId, DateTimeOffset createdAtUtc)
    {
        WorkspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        ConversationId = conversationId;
        AgentId = agentId;
        NativeSessionId = nativeSessionId;
        Status = ConversationSessionStatus.Idle;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        LastUsedAtUtc = CreatedAtUtc;
    }

    public string WorkspaceId { get; }
    public string ConversationId { get; }
    public string AgentId { get; }
    public string NativeSessionId { get; private set; }
    public ConversationSessionStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset LastUsedAtUtc { get; private set; }
    public DateTimeOffset? InvalidatedAtUtc { get; private set; }

    public static ConversationSession Create(string conversationId, string agentId, string nativeSessionId, DateTimeOffset createdAtUtc)
        => Create(WorkspaceIdentifier.Default, conversationId, agentId, nativeSessionId, createdAtUtc);

    public static ConversationSession Create(string workspaceId, string conversationId, string agentId, string nativeSessionId, DateTimeOffset createdAtUtc)
    {
        workspaceId = WorkspaceIdentifier.Normalize(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        return new ConversationSession(workspaceId, conversationId, agentId, nativeSessionId, createdAtUtc);
    }

    public static ConversationSession Restore(
        string conversationId,
        string agentId,
        string nativeSessionId,
        ConversationSessionStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastUsedAtUtc,
        DateTimeOffset? invalidatedAtUtc)
        => Restore(WorkspaceIdentifier.Default, conversationId, agentId, nativeSessionId, status, createdAtUtc, lastUsedAtUtc, invalidatedAtUtc);

    public static ConversationSession Restore(
        string workspaceId,
        string conversationId,
        string agentId,
        string nativeSessionId,
        ConversationSessionStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastUsedAtUtc,
        DateTimeOffset? invalidatedAtUtc)
    {
        var session = Create(workspaceId, conversationId, agentId, nativeSessionId, createdAtUtc);
        session.Status = status;
        session.LastUsedAtUtc = lastUsedAtUtc.ToUniversalTime();
        session.InvalidatedAtUtc = invalidatedAtUtc?.ToUniversalTime();
        return session;
    }

    public void StartRun(DateTimeOffset occurredAtUtc)
    {
        if (Status != ConversationSessionStatus.Idle)
        {
            throw new InvalidOperationException("Only idle sessions can start a run.");
        }

        Status = ConversationSessionStatus.Running;
        LastUsedAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public void CompleteRun(string nativeSessionId, DateTimeOffset occurredAtUtc)
    {
        if (Status != ConversationSessionStatus.Running)
        {
            throw new InvalidOperationException("Only running sessions can complete a run.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        NativeSessionId = nativeSessionId;
        Status = ConversationSessionStatus.Idle;
        LastUsedAtUtc = occurredAtUtc.ToUniversalTime();
    }

    public void Invalidate(DateTimeOffset occurredAtUtc)
    {
        if (Status == ConversationSessionStatus.Invalid)
        {
            return;
        }

        Status = ConversationSessionStatus.Invalid;
        InvalidatedAtUtc = occurredAtUtc.ToUniversalTime();
        LastUsedAtUtc = InvalidatedAtUtc.Value;
    }
}
