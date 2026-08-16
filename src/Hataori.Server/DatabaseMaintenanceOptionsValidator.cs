using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>Database Maintenance設定を検証します。</summary>
public sealed class DatabaseMaintenanceOptionsValidator : IValidateOptions<DatabaseMaintenanceOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseMaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IntervalHours is < 1 or > 720 || options.StaleTaskHours is < 1 or > 8760 || options.TaskRetentionDays is < 1 or > 3650 || options.AgentRunRetentionDays is < 1 or > 3650 || options.MessageRetentionDays is < 1 or > 3650
            ? ValidateOptionsResult.Fail("Database Maintenance periods are outside the supported range.")
            : ValidateOptionsResult.Success;
    }
}
