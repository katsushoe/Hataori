using Hataori.Infrastructure.Agents.ClaudeCode;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class ClaudeCodeDriverOptionsValidator : IValidateOptions<ClaudeCodeDriverOptions>
{
    public ValidateOptionsResult Validate(string? name, ClaudeCodeDriverOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            errors.Add("Claude Code executablePath is required.");
        }

        if (options.PermissionMode is not ("acceptEdits" or "plan"))
        {
            errors.Add("Claude Code permissionMode must be acceptEdits or plan.");
        }

        if (options.MaxCapturedCharacters is < 1024 or > 16 * 1024 * 1024)
        {
            errors.Add("Claude Code maxCapturedCharacters must be between 1024 and 16777216.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
