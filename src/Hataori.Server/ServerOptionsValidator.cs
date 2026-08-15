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

        return ValidateOptionsResult.Success;
    }
}
