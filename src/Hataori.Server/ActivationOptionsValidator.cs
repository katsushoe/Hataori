using Microsoft.Extensions.Options;
using Hataori.Core.Workspaces;

namespace Hataori.Server;

public sealed class ActivationOptionsValidator : IValidateOptions<ActivationOptions>
{
    public ValidateOptionsResult Validate(string? name, ActivationOptions options)
    {
        var errors = new List<string>();
        IReadOnlyList<ActivationWorkspace> workspaces = [];
        try
        {
            workspaces = ActivationWorkspaceResolver.Resolve(options);
        }
        catch (ArgumentException exception)
        {
            errors.Add(exception.Message);
        }

        if (options.Workspaces.Count > 0 && !string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            errors.Add("Activation workspaces cannot be combined with the legacy workingDirectory.");
        }

        if (workspaces.Select(workspace => workspace.WorkspaceId).Distinct(StringComparer.Ordinal).Count() != workspaces.Count)
        {
            errors.Add("Activation workspace IDs must be unique.");
        }

        var configuredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var workspace in workspaces)
        {
            if (options.Enabled && (string.IsNullOrWhiteSpace(workspace.WorkingDirectory) || !Path.IsPathFullyQualified(workspace.WorkingDirectory)))
            {
                errors.Add($"Activation workspace '{workspace.WorkspaceId}' workingDirectory must be an absolute path when activation is enabled.");
                continue;
            }
            if (!options.Enabled)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(workspace.WorkingDirectory);
            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Activation workspace '{workspace.WorkspaceId}' workingDirectory must exist when activation is enabled.");
                continue;
            }
            if (!configuredPaths.Add(fullPath))
            {
                errors.Add("Activation workspace workingDirectory values must be unique.");
            }
            foreach (var directory in Directory.EnumerateDirectories(fullPath))
            {
                var projectId = Path.GetFileName(directory).ToLowerInvariant();
                if (!projectIds.Add(projectId))
                {
                    errors.Add($"Activation project ID '{projectId}' exists in more than one workspace.");
                }
            }
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
