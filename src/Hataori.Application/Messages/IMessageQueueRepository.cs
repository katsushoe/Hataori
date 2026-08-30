using Hataori.Core.Messages;

namespace Hataori.Application.Messages;

public interface IMessageQueueRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueAsync(IncomingMessage message, int priority, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueuedMessage>> ListAsync(string? agentId, CancellationToken cancellationToken);
    Task<QueuedMessage?> GetQueuedAsync(string messageId, CancellationToken cancellationToken);
    Task<QueuedMessage> RetryAsync(string messageId, DateTimeOffset enqueuedAtUtc, CancellationToken cancellationToken);
    Task CancelQueuedAsync(string messageId, DateTimeOffset cancelledAtUtc, CancellationToken cancellationToken);
    Task<QueuedMessage?> TryClaimNextAsync(string? agentId, CancellationToken cancellationToken);
    Task MarkRunningAsync(string messageId, CancellationToken cancellationToken);
    Task MarkRespondedAsync(string messageId, string replyMessageId, DateTimeOffset respondedAtUtc, CancellationToken cancellationToken);
    Task MarkFailedAsync(string messageId, string error, DateTimeOffset failedAtUtc, CancellationToken cancellationToken);
    Task<MessageProcessingStatus?> GetProcessingStatusAsync(string messageId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetActiveExecutionMessageIdsAsync(CancellationToken cancellationToken);
    Task ScheduleReplyRetryAsync(string messageId, string error, int attemptCount, DateTimeOffset failedAtUtc, DateTimeOffset? nextAttemptAtUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingReply>> GetDueReplyRetriesAsync(DateTimeOffset dueAtUtc, int limit, CancellationToken cancellationToken);
}
