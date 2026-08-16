using Hataori.Infrastructure.Maintenance;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>設定周期でSQLite Maintenanceを実行します。</summary>
public sealed class DatabaseMaintenanceWorker(SqliteDatabaseMaintenance maintenance, StartupRecoveryGate recoveryGate, IOptions<DatabaseMaintenanceOptions> options, ILogger<DatabaseMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await recoveryGate.Ready.WaitAsync(stoppingToken).ConfigureAwait(false))
        {
            logger.LogWarning("[Recovery] Database maintenance was not started because startup recovery failed");
            return;
        }
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.Value.IntervalHours));
        do
        {
            try
            {
                var value = options.Value;
                var settings = new DatabaseMaintenanceSettings(TimeSpan.FromHours(value.StaleTaskHours), TimeSpan.FromDays(value.TaskRetentionDays), TimeSpan.FromDays(value.AgentRunRetentionDays), TimeSpan.FromDays(value.MessageRetentionDays), value.Vacuum);
                var result = await maintenance.ExecuteAsync(settings, stoppingToken).ConfigureAwait(false);
                logger.LogInformation("[Maintenance] Completed: expired={ExpiredTasks} tasks={PurgedTasks} runs={PurgedRuns} messages={PurgedMessages} vacuum={Vacuumed}", result.ExpiredTasks, result.PurgedTasks, result.PurgedAgentRuns, result.PurgedMessages, result.Vacuumed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (exception is SqliteException or IOException)
            {
                logger.LogError(exception, "[Maintenance] Database maintenance failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
