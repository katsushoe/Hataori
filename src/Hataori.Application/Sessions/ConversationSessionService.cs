using Hataori.Core.Sessions;

namespace Hataori.Application.Sessions;

public sealed class ConversationSessionService(IConversationSessionRepository repository, TimeProvider timeProvider)
{
    public async Task<ConversationSession> RegisterAsync(string conversationId, string agentId, string nativeSessionId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Status != ConversationSessionStatus.Invalid)
        {
            throw new InvalidOperationException("An active conversation session is already registered.");
        }

        var session = ConversationSession.Create(conversationId, agentId, nativeSessionId, timeProvider.GetUtcNow());
        await repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    public Task<ConversationSession?> GetAsync(string conversationId, string agentId, CancellationToken cancellationToken) =>
        repository.GetAsync(conversationId, agentId, cancellationToken);

    public Task<IReadOnlyList<ConversationSession>> ListAsync(ConversationSessionStatus? status, string? agentId, CancellationToken cancellationToken) =>
        repository.ListAsync(status, agentId, cancellationToken);

    public async Task<ConversationSession> StartRunAsync(string conversationId, string agentId, CancellationToken cancellationToken)
    {
        var session = await GetRequiredAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false);
        session.StartRun(timeProvider.GetUtcNow());
        await repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task<ConversationSession> CompleteRunAsync(string conversationId, string agentId, string nativeSessionId, CancellationToken cancellationToken)
    {
        var session = await GetRequiredAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false);
        session.CompleteRun(nativeSessionId, timeProvider.GetUtcNow());
        await repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    public async Task<ConversationSession> InvalidateAsync(string conversationId, string agentId, CancellationToken cancellationToken)
    {
        var session = await GetRequiredAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false);
        session.Invalidate(timeProvider.GetUtcNow());
        await repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    private async Task<ConversationSession> GetRequiredAsync(string conversationId, string agentId, CancellationToken cancellationToken) =>
        await repository.GetAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false)
        ?? throw new KeyNotFoundException($"Conversation session '{conversationId}/{agentId}' was not found.");
}
