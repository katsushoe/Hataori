using Hataori.Application.Activation;
using Hataori.Application.Messages;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ActivationWorker(
    ActivationManager activationManager,
    IMessageQueueRepository messageQueue,
    IOptions<ActivationOptions> activationOptions,
    IOptions<ServerOptions> serverOptions,
    ILogger<ActivationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!activationOptions.Value.Enabled)
        {
            logger.LogInformation("Activation Manager is disabled");
            return;
        }

        await messageQueue.InitializeAsync(stoppingToken).ConfigureAwait(false);
        var server = serverOptions.Value;
        var mcpUrl = $"http://{server.McpHost}:{server.McpPort}{server.McpPath}";
        var request = new ActivationRequest(activationOptions.Value.WorkingDirectory, AppContext.BaseDirectory, mcpUrl);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await activationManager.ProcessNextAsync(request, stoppingToken).ConfigureAwait(false);
                if (result is null)
                {
                    await Task.Delay(activationOptions.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
                }
                else if (!result.Succeeded)
                {
                    logger.LogWarning("Activation failed for message {MessageId}: {Error}", result.MessageId, result.Error);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Activation loop failed");
                await Task.Delay(activationOptions.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
