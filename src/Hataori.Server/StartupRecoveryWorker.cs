namespace Hataori.Server;

/// <summary>Server起動時に永続状態の復旧を1回実行します。</summary>
public sealed class StartupRecoveryWorker(
    StartupRecoveryService recovery,
    DatabaseInitializationGate initializationGate,
    StartupRecoveryGate gate,
    IHostApplicationLifetime applicationLifetime,
    ILogger<StartupRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await initializationGate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false))
        {
            gate.Fail();
            logger.LogWarning(Hataori.Application.Localization.DisplayLanguage.Text("[起動][データベース] データベース初期化に失敗したため起動時リカバリを開始しませんでした", "[Startup][Database] Startup recovery was not started because database initialization failed"));
            return;
        }

        try
        {
            var result = await recovery.RecoverAsync(stoppingToken).ConfigureAwait(false);
            logger.LogInformation(Hataori.Application.Localization.DisplayLanguage.Text("[リカバリ] 完了: 失敗run={FailedRuns} 失敗message={FailedMessages} 無効session={InvalidatedSessions} 継続run={SurvivingRuns}", "[Recovery] Completed: failed_runs={FailedRuns} failed_messages={FailedMessages} invalidated_sessions={InvalidatedSessions} surviving_runs={SurvivingRuns}"), result.FailedRuns, result.FailedMessages, result.InvalidatedSessions, result.SurvivingRuns);
            gate.Complete();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            gate.Fail();
        }
        catch (Exception exception)
        {
            gate.Fail();
            logger.LogCritical(
                exception,
                "[Recovery] Startup recovery failed. Check database access, available disk space, and the configured database path. Hataori will stop safely");
            applicationLifetime.StopApplication();
        }
    }
}
