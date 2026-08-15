using System.Net;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>
/// Server設定を起動前に検証します。
/// </summary>
public sealed class ServerOptionsValidator : IValidateOptions<ServerOptions>
{
    public ValidateOptionsResult Validate(string? name, ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return ValidateOptionsResult.Fail("server:databasePath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ControlPipeName) || options.ControlPipeName.IndexOfAny(['\\', '/']) >= 0)
        {
            return ValidateOptionsResult.Fail("server:controlPipeName must be a valid pipe name.");
        }

        if (!IPAddress.TryParse(options.McpHost, out var address) || !IPAddress.IsLoopback(address))
        {
            return ValidateOptionsResult.Fail("server:mcpHost must be a loopback IP address.");
        }

        if (options.McpPort is < 1 or > 65535)
        {
            return ValidateOptionsResult.Fail("server:mcpPort must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(options.McpPath) || options.McpPath[0] != '/')
        {
            return ValidateOptionsResult.Fail("server:mcpPath must begin with '/'.");
        }

        return ValidateOptionsResult.Success;
    }
}
