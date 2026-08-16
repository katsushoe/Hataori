namespace Hataori.Server;

/// <summary>Server起動時に永続状態の復旧を1回実行します。</summary>
public sealed class StartupRecoveryWorker(StartupRecoveryService recovery, StartupRecoveryGate gate, ILogger<StartupRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var result = await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
            logger.LogInformation("[Recovery] Completed: failed_runs={FailedRuns} failed_messages={FailedMessages} invalidated_sessions={InvalidatedSessions} surviving_runs={SurvivingRuns}", result.FailedRuns, result.FailedMessages, result.InvalidatedSessions, result.SurvivingRuns);
            gate.Complete();
        }
        catch (Exception exception)
        {
            gate.Fail(exception);
            logger.LogCritical(exception, "[Recovery] Startup recovery failed");
            throw;
        }
    }
}
