using Hataori.Infrastructure.Itoguruma;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ItogurumaClientOptionsValidator : IValidateOptions<ItogurumaClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ItogurumaClientOptions options)
    {
        // AuthenticationTokenは意図的に必須としない。未設定の場合、ItogurumaConnectionWorkerが
        // 接続失敗としてdegraded状態を報告し続けるだけで、Hataori自体はItoguruma未連携でも起動できる。
        var errors = new List<string>();
        if (options.Endpoint is null || !options.Endpoint.IsAbsoluteUri)
        {
            errors.Add("Itoguruma endpoint must be an absolute URI.");
        }
        else if (!options.Endpoint.IsLoopback || (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Itoguruma endpoint must be an HTTP(S) loopback URI.");
        }

        if (string.IsNullOrWhiteSpace(options.AgentId) || string.IsNullOrWhiteSpace(options.AgentType))
        {
            errors.Add("Itoguruma agentId and agentType are required.");
        }

        if (options.MonitoredAgentIds.Count == 0 || options.MonitoredAgentIds.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Itoguruma monitoredAgentIds must contain at least one non-empty agent ID.");
        }

        if (options.MonitoredAgentIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.MonitoredAgentIds.Count)
        {
            errors.Add("Itoguruma monitoredAgentIds must not contain duplicates.");
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
