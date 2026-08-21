using Hataori.Application.Activation;
using Hataori.Application.Messages;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ReplyRetryWorker(
    ReplyRetryManager retryManager,
    IMessageQueueRepository messageQueue,
    DatabaseInitializationGate initializationGate,
    StartupRecoveryGate recoveryGate,
    IOptions<ReplyRetryOptions> options,
    TimeProvider timeProvider,
    ILogger<ReplyRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation(Hataori.Application.Localization.DisplayLanguage.Text("Reply retry workerは無効です", "Reply retry worker is disabled"));
            return;
        }

        if (!await initializationGate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false)
            || !await recoveryGate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false))
        {
            logger.LogWarning(Hataori.Application.Localization.DisplayLanguage.Text("[起動] データベース初期化または起動時リカバリに失敗したためReply retryを開始しませんでした", "[Startup] Reply retry was not started because database initialization or startup recovery failed"));
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
                    logger.LogWarning(Hataori.Application.Localization.DisplayLanguage.Text("Reply retry batchが{Failed}件失敗し、{Exhausted}件が上限へ到達しました", "Reply retry batch failed {Failed} times; exhausted {Exhausted}"), result.Failed, result.Exhausted);
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
