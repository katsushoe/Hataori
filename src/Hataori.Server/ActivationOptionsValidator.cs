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

        if (options.Enabled && options.MaxConcurrentRuns.Count == 0)
        {
            errors.Add("Activation maxConcurrentRuns must contain at least one agent when activation is enabled.");
        }

        if (options.MaxConcurrentRuns.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is < 1 or > 32))
        {
            errors.Add("Activation maxConcurrentRuns keys must be non-empty and values must be between 1 and 32.");
        }

        if (options.ProviderPriority.Count == 0 || options.ProviderPriority.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Activation providerPriority must contain at least one non-empty provider.");
        }
        else if (options.ProviderPriority.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.ProviderPriority.Count)
        {
            errors.Add("Activation providerPriority must not contain duplicates.");
        }

        if (options.Enabled && options.ProviderPriority.Any(provider => !options.MaxConcurrentRuns.ContainsKey(provider)))
        {
            errors.Add("Activation providerPriority entries must exist in maxConcurrentRuns.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
