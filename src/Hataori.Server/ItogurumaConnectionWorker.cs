using Hataori.Application.Itoguruma;
using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>
/// Itogurumaへの接続を監視し、切断時に再接続します。
/// </summary>
public sealed class ItogurumaConnectionWorker(
    IItogurumaClient client,
    IOptions<ItogurumaClientOptions> options,
    ILogger<ItogurumaConnectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(stoppingToken).ConfigureAwait(false);
                var status = await client.GetStatusAsync(stoppingToken).ConfigureAwait(false);
                if (failures > 0)
                {
                    logger.LogInformation("Itoguruma connection recovered: {Name} {Version}", status.Name, status.Version);
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
                failures++;
                logger.LogWarning(exception, "Itoguruma connection failed ({Attempt}/{MaximumAttempts})", failures, options.Value.MaxReconnectAttempts);
                await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
                if (failures >= options.Value.MaxReconnectAttempts)
                {
                    throw new InvalidOperationException("Itoguruma reconnect attempts were exhausted.", exception);
                }

                var delaySeconds = Math.Min(options.Value.PollIntervalSeconds, 1 << Math.Min(failures - 1, 6));
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
