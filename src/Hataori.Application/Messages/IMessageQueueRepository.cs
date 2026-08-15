using Hataori.Core.Messages;

namespace Hataori.Application.Messages;

public interface IMessageQueueRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueAsync(IncomingMessage message, int priority, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueuedMessage>> ListAsync(string? agentId, CancellationToken cancellationToken);
    Task<QueuedMessage?> TryClaimNextAsync(string? agentId, CancellationToken cancellationToken);
}
