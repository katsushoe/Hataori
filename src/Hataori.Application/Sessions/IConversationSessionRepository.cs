using Hataori.Core.Sessions;

namespace Hataori.Application.Sessions;

public interface IConversationSessionRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<ConversationSession?> GetAsync(string conversationId, string agentId, CancellationToken cancellationToken);
    Task<ConversationSession?> GetAsync(string workspaceId, string conversationId, string agentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationSession>> ListAsync(ConversationSessionStatus? status, string? agentId, CancellationToken cancellationToken);
    Task SaveAsync(ConversationSession session, CancellationToken cancellationToken);
}
