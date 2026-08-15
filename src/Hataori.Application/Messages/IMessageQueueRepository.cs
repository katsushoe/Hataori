using Hataori.Core.Messages;

namespace Hataori.Application.Messages;

public interface IMessageQueueRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueAsync(IncomingMessage message, int priority, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueuedMessage>> ListAsync(string? agentId, CancellationToken cancellationToken);
    Task<QueuedMessage?> TryClaimNextAsync(string? agentId, CancellationToken cancellationToken);
    Task MarkRunningAsync(string messageId, CancellationToken cancellationToken);
    Task MarkRespondedAsync(string messageId, DateTimeOffset respondedAtUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(string messageId, string error, DateTimeOffset failedAtUtc, CancellationToken cancellationToken);
    Task<MessageProcessingStatus?> GetProcessingStatusAsync(string messageId, CancellationToken cancellationToken);
}
