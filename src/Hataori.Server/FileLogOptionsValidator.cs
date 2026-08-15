using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>
/// ファイルログ設定を検証します。
/// </summary>
public sealed class FileLogOptionsValidator : IValidateOptions<FileLogOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, FileLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            errors.Add("File logging directoryPath is required.");
        }

        if (!Enum.TryParse<LogLevel>(options.MinimumLevel, true, out var level) || level == LogLevel.None)
        {
            errors.Add("File logging minimumLevel is invalid.");
        }

        if (options.RetentionDays is < 1 or > 3650)
        {
            errors.Add("File logging retentionDays must be between 1 and 3650.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
