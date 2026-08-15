using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ItogurumaClientOptionsValidator : IValidateOptions<ItogurumaClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ItogurumaClientOptions options)
    {
        var errors = new List<string>();
        if (options.Endpoint is null || !options.Endpoint.IsAbsoluteUri)
        {
            errors.Add("Itoguruma endpoint must be an absolute URI.");
        }
        else if (!options.Endpoint.IsLoopback || (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Itoguruma endpoint must be an HTTP(S) loopback URI.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthenticationToken))
        {
            errors.Add("Itoguruma authentication token is required.");
        }

        if (string.IsNullOrWhiteSpace(options.AgentId) || string.IsNullOrWhiteSpace(options.AgentType))
        {
            errors.Add("Itoguruma agentId and agentType are required.");
        }

        if (options.ConnectionTimeoutSeconds is < 1 or > 120)
        {
            errors.Add("Itoguruma connectionTimeoutSeconds must be between 1 and 120.");
        }

        if (options.PollIntervalSeconds is < 1 or > 300)
        {
            errors.Add("Itoguruma pollIntervalSeconds must be between 1 and 300.");
        }

        if (options.MaxReconnectAttempts is < 1 or > 100)
        {
            errors.Add("Itoguruma maxReconnectAttempts must be between 1 and 100.");
        }

        if (options.ReceiveBatchSize is < 1 or > 500)
        {
            errors.Add("Itoguruma receiveBatchSize must be between 1 and 500.");
        }

        if (options.LeaseSeconds is < 1 or > 3600)
        {
            errors.Add("Itoguruma leaseSeconds must be between 1 and 3600.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
