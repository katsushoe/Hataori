using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ReplyRetryOptionsValidator : IValidateOptions<ReplyRetryOptions>
{
    public ValidateOptionsResult Validate(string? name, ReplyRetryOptions options)
    {
        var errors = new List<string>();
        if (options.MaxAttempts is < 1 or > 20)
        {
            errors.Add("Reply retry maxAttempts must be between 1 and 20.");
        }

        if (options.InitialDelaySeconds is < 1 or > 3600 || options.MaximumDelaySeconds < options.InitialDelaySeconds || options.MaximumDelaySeconds > 86400)
        {
            errors.Add("Reply retry delays are invalid.");
        }

        if (options.BatchSize is < 1 or > 500)
        {
            errors.Add("Reply retry batchSize must be between 1 and 500.");
        }

        if (options.PollIntervalMilliseconds is < 100 or > 60000)
        {
            errors.Add("Reply retry pollIntervalMilliseconds must be between 100 and 60000.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
