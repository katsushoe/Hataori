using Hataori.Application.Itoguruma;
using Hataori.Application.Messages;

namespace Hataori.Application.Activation;

public sealed record ReplyRetrySettings(int MaxAttempts, TimeSpan InitialDelay, TimeSpan MaximumDelay, int BatchSize);
public sealed record ReplyRetryBatchResult(int Succeeded, int Failed, int Exhausted);

public sealed class ReplyRetryManager(IMessageQueueRepository messageQueue, IItogurumaClient itoguruma, ReplyRetrySettings settings)
{
    public Task ScheduleInitialFailureAsync(string messageId, string error, DateTimeOffset failedAtUtc, CancellationToken cancellationToken)
    {
        DateTimeOffset? nextAttempt = settings.MaxAttempts <= 1 ? null : failedAtUtc + CalculateDelay(1);
        return messageQueue.ScheduleReplyRetryAsync(messageId, error, 1, failedAtUtc, nextAttempt, cancellationToken);
    }

    public async Task<ReplyRetryBatchResult> ProcessDueAsync(DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
    {
        var pending = await messageQueue.GetDueReplyRetriesAsync(dueAtUtc, settings.BatchSize, cancellationToken).ConfigureAwait(false);
        var succeeded = 0;
        var failed = 0;
        var exhausted = 0;
        foreach (var reply in pending)
        {
            try
            {
                var replyMessageId = await itoguruma.ReplyAsync(
                    reply.RecipientAgentId,
                    reply.FinalMessage,
                    reply.ConversationId,
                    reply.MessageId,
                    ReplyIdempotencyKey.Create(reply.MessageId),
                    cancellationToken).ConfigureAwait(false);
                await messageQueue.MarkRespondedAsync(reply.MessageId, replyMessageId, dueAtUtc, cancellationToken).ConfigureAwait(false);
                succeeded++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var attempt = reply.AttemptCount + 1;
                DateTimeOffset? nextAttempt = attempt >= settings.MaxAttempts ? null : dueAtUtc + CalculateDelay(attempt);
                await messageQueue.ScheduleReplyRetryAsync(reply.MessageId, exception.Message, attempt, dueAtUtc, nextAttempt, cancellationToken).ConfigureAwait(false);
                failed++;
                if (nextAttempt is null)
                {
                    exhausted++;
                }
            }
        }

        return new ReplyRetryBatchResult(succeeded, failed, exhausted);
    }

    private TimeSpan CalculateDelay(int attemptCount)
    {
        var multiplier = 1L << Math.Min(attemptCount - 1, 20);
        var ticks = Math.Min(settings.InitialDelay.Ticks * multiplier, settings.MaximumDelay.Ticks);
        return TimeSpan.FromTicks(ticks);
    }
}
