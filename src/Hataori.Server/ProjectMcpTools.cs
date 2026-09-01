using System.ComponentModel;
using Hataori.Application.Activation;
using Hataori.Core.Workspaces;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>AI Agentへ公開するProject検索ツールです。</summary>
[McpServerToolType]
public sealed class ProjectMcpTools
{
    private readonly AgentProviderSelector _selector;
    private readonly ActivationOptions _options;

    public ProjectMcpTools(AgentProviderSelector selector, IOptions<ActivationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(options);
        _selector = selector;
        _options = options.Value;
    }

    /// <summary>登録済みProject IDを、任意の部分一致queryで絞り込んで返します。</summary>
    [McpServerTool(Name = "list_projects", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists registered project IDs. Call this before task registration to resolve the intended project name. An optional query filters IDs by case-insensitive substring match.")]
    public IReadOnlyList<string> List(string? query)
    {
        var projects = DiscoverWorkspaces()
            .SelectMany(workspace => workspace.ProjectIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(project => project, StringComparer.OrdinalIgnoreCase)
            .AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            projects = projects.Where(project => project.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return projects.ToArray();
    }

    /// <summary>設定済みWorkspaceと、その配下のProject IDを返します。</summary>
    [McpServerTool(Name = "list_workspaces", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists configured workspaces and their registered project IDs. A workspace is the configured projects root; projects are its immediate child directories.")]
    public IReadOnlyList<WorkspaceDescriptor> ListWorkspaces()
        => DiscoverWorkspaces();

    private IReadOnlyList<WorkspaceDescriptor> DiscoverWorkspaces() =>
        ActivationWorkspaceResolver.Resolve(_options)
            .Select(workspace =>
            {
                var path = string.IsNullOrWhiteSpace(workspace.WorkingDirectory)
                    ? null
                    : Path.GetFullPath(workspace.WorkingDirectory);
                var projects = path is null || !Directory.Exists(path)
                    ? []
                    : _selector.DiscoverProjects(path).Select(project => project.ProjectId).ToArray();
                return new WorkspaceDescriptor(workspace.WorkspaceId, path, projects);
            })
            .ToArray();
}

/// <summary>設定済みWorkspaceとProject IDの読み取り専用表現です。</summary>
public sealed record WorkspaceDescriptor(string WorkspaceId, string? WorkingDirectory, IReadOnlyList<string> ProjectIds);
