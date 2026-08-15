using Hataori.Application.Activation;
using Hataori.Application.Messages;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ReplyRetryWorker(
    ReplyRetryManager retryManager,
    IMessageQueueRepository messageQueue,
    IOptions<ReplyRetryOptions> options,
    TimeProvider timeProvider,
    ILogger<ReplyRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Reply retry worker is disabled");
            return;
        }

        await messageQueue.InitializeAsync(stoppingToken).ConfigureAwait(false);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await retryManager.ProcessDueAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
                if (result.Failed > 0)
                {
                    logger.LogWarning("Reply retry batch failed {Failed} times; exhausted {Exhausted}", result.Failed, result.Exhausted);
                }

                await Task.Delay(options.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reply retry loop failed");
                await Task.Delay(options.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
