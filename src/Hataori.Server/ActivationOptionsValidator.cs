using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ActivationOptionsValidator : IValidateOptions<ActivationOptions>
{
    public ValidateOptionsResult Validate(string? name, ActivationOptions options)
    {
        var errors = new List<string>();
        if (options.Enabled && (string.IsNullOrWhiteSpace(options.WorkingDirectory) || !Path.IsPathFullyQualified(options.WorkingDirectory)))
        {
            errors.Add("Activation workingDirectory must be an absolute path when activation is enabled.");
        }
        else if (options.Enabled && !Directory.Exists(options.WorkingDirectory))
        {
            errors.Add("Activation workingDirectory must exist when activation is enabled.");
        }

        if (options.PollIntervalMilliseconds is < 100 or > 60000)
        {
            errors.Add("Activation pollIntervalMilliseconds must be between 100 and 60000.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
