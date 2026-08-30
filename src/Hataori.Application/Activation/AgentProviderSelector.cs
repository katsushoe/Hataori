using Hataori.Application.Agents;

namespace Hataori.Application.Activation;

/// <summary>受信元Providerと設定優先順位から対象プロジェクトを実行するProviderを選択します。</summary>
public sealed class AgentProviderSelector(IEnumerable<IAgentDriver> drivers)
{
    private readonly HashSet<string> _availableProviders = drivers
        .Select(driver => driver.AgentType)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>対象プロジェクトの作業ディレクトリとProviderを解決します。</summary>
    public ProviderSelection Select(string projectsRoot, string projectId, string? sourceProvider, IReadOnlyList<string> providerPriority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(providerPriority);
        if (Path.IsPathFullyQualified(projectId) || projectId.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Project ID must be a directory name.", nameof(projectId));
        }

        var root = Path.GetFullPath(projectsRoot);
        var projectPath = Directory.EnumerateDirectories(root)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), projectId, StringComparison.OrdinalIgnoreCase));
        if (projectPath is null)
        {
            var projectCandidates = DiscoverProjects(root).Select(project => project.ProjectId);
            throw new DirectoryNotFoundException($"Project '{projectId}' is not registered. Registered project candidates: {string.Join(", ", projectCandidates)}.");
        }

        var candidates = new List<string>();
        AddCandidate(candidates, sourceProvider);
        foreach (var provider in providerPriority)
        {
            AddCandidate(candidates, provider);
        }

        var selected = candidates.FirstOrDefault(_availableProviders.Contains)
            ?? throw new InvalidOperationException($"No configured provider can open project '{projectId}'.");
        return new ProviderSelection(selected, projectPath);
    }

    /// <summary>Projects root直下にある起動対象プロジェクトを列挙します。</summary>
    public IReadOnlyList<ActivationProject> DiscoverProjects(string projectsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectsRoot);
        var root = Path.GetFullPath(projectsRoot);
        return Directory.EnumerateDirectories(root)
            .Select(path => new ActivationProject(NormalizeProjectId(Path.GetFileName(path)), path))
            .OrderBy(project => project.ProjectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeProjectId(string directoryName) => directoryName.ToLowerInvariant();

    private static void AddCandidate(List<string> candidates, string? provider)
    {
        if (!string.IsNullOrWhiteSpace(provider) && !candidates.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(provider);
        }
    }
}

/// <summary>Provider選択結果です。</summary>
public sealed record ProviderSelection(string Provider, string ProjectPath);

/// <summary>自動検出された起動対象プロジェクトです。</summary>
public sealed record ActivationProject(string ProjectId, string ProjectPath);
