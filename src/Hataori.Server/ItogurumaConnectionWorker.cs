using Hataori.Application.Itoguruma;
using Hataori.Application.Messages;
using Hataori.Core.Messages;
using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>
/// Itogurumaへの接続を監視し、切断時に再接続します。
/// </summary>
public sealed class ItogurumaConnectionWorker(
    IItogurumaClient client,
    IMessageQueueRepository messageQueue,
    ItogurumaConnectionState connectionState,
    IOptions<ItogurumaClientOptions> options,
    ILogger<ItogurumaConnectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        connectionState.Set("connecting");
        try
        {
            await messageQueue.InitializeAsync(stoppingToken).ConfigureAwait(false);
            var failures = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await client.ConnectAsync(stoppingToken).ConfigureAwait(false);
                    var status = await client.GetStatusAsync(stoppingToken).ConfigureAwait(false);
                    connectionState.Set("connected");
                    foreach (var agentId in options.Value.MonitoredAgentIds)
                    {
                        var messages = await client.GetMessagesAsync(
                            agentId,
                            options.Value.ReceiveBatchSize,
                            options.Value.LeaseSeconds,
                            null,
                            stoppingToken).ConfigureAwait(false);
                        foreach (var message in messages)
                        {
                            await PersistAndAcknowledgeAsync(agentId, message, stoppingToken).ConfigureAwait(false);
                        }
                    }
                    if (failures > 0)
                    {
                        logger.LogInformation(Hataori.Application.Localization.DisplayLanguage.Text("Itoguruma接続が復旧しました: {Name} {Version}", "Itoguruma connection recovered: {Name} {Version}"), status.Name, status.Version);
                    }

                    failures = 0;
                    await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    connectionState.Set("degraded");
                    failures++;
                    logger.LogWarning(exception, "Itoguruma connection failed ({Attempt}/{MaximumAttempts})", failures, options.Value.MaxReconnectAttempts);
                    await SafeDisconnectAsync().ConfigureAwait(false);
                    if (failures >= options.Value.MaxReconnectAttempts)
                    {
                        logger.LogError(
                            exception,
                            "[Itoguruma] Reconnect limit reached. Hataori will continue in degraded mode and retry. Check the endpoint, network connection, and authentication token");
                        failures = 0;
                        await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    var delaySeconds = Math.Min(options.Value.PollIntervalSeconds, 1 << Math.Min(failures - 1, 6));
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            connectionState.Set("stopped");
        }
    }

    private async Task SafeDisconnectAsync()
    {
        try
        {
            await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Itoguruma disconnect failed while recovering from a connection error");
        }
    }

    private async Task PersistAndAcknowledgeAsync(string agentId, ItogurumaMessage message, CancellationToken cancellationToken)
    {
        var incoming = new IncomingMessage(
            message.MessageId,
            message.ThreadId,
            agentId,
            message.SenderAgentId,
            message.ReplyToMessageId,
            message.MessageType,
            message.Body,
            message.PayloadJson,
            message.CreatedAt);
        var inserted = await messageQueue.EnqueueAsync(incoming, 0, cancellationToken).ConfigureAwait(false);
        var acknowledged = await client.AcknowledgeAsync(agentId, message.MessageId, cancellationToken).ConfigureAwait(false);
        if (!acknowledged)
        {
            throw new InvalidOperationException($"Itoguruma message '{message.MessageId}' could not be acknowledged.");
        }

        logger.LogInformation(
            inserted ? "Itoguruma message queued: {MessageId}" : "Duplicate Itoguruma message acknowledged: {MessageId}",
            message.MessageId);
    }
}
