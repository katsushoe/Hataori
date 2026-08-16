using Hataori.Infrastructure.Agents.Codex;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

public sealed class CodexDriverOptionsValidator : IValidateOptions<CodexDriverOptions>
{
    public ValidateOptionsResult Validate(string? name, CodexDriverOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            errors.Add("Codex executablePath is required.");
        }

        if (options.SandboxMode is not ("read-only" or "workspace-write"))
        {
            errors.Add("Codex sandboxMode must be read-only or workspace-write.");
        }

        if (options.ApproveForMe && options.SandboxMode != "workspace-write")
        {
            errors.Add("Codex approveForMe requires workspace-write because the CLI applies that sandbox automatically.");
        }

        if (options.MaxCapturedCharacters is < 1024 or > 16 * 1024 * 1024)
        {
            errors.Add("Codex maxCapturedCharacters must be between 1024 and 16777216.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
