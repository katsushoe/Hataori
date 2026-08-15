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
        var lanes = ActivationLanePlan.Create(activationOptions.Value.MaxConcurrentRuns);
        logger.LogInformation("Activation Manager started with {LaneCount} lanes", lanes.Count);
        await Task.WhenAll(lanes.Select((agentId, index) => RunLaneAsync(agentId, index + 1, request, stoppingToken))).ConfigureAwait(false);
    }

    private async Task RunLaneAsync(string agentId, int laneNumber, ActivationRequest request, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await activationManager.ProcessNextAsync(request, agentId, stoppingToken).ConfigureAwait(false);
                if (result is null)
                {
                    await Task.Delay(activationOptions.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
                }
                else if (!result.Succeeded)
                {
                    logger.LogWarning("Activation failed for agent {AgentId} lane {LaneNumber}, run {RunId}, message {MessageId}: {Error}", agentId, laneNumber, result.RunId, result.MessageId, result.Error);
                }
                else
                {
                    logger.LogInformation("Activation completed for agent {AgentId}, run {RunId}, message {MessageId}, reply {ReplyMessageId}", agentId, result.RunId, result.MessageId, result.ReplyMessageId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Activation lane failed for agent {AgentId}, lane {LaneNumber}", agentId, laneNumber);
                await Task.Delay(activationOptions.Value.PollIntervalMilliseconds, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
