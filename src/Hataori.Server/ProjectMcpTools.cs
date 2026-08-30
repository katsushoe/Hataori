using System.ComponentModel;
using Hataori.Application.Activation;
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
        var projects = _selector.DiscoverProjects(_options.WorkingDirectory)
            .Select(project => project.ProjectId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            projects = projects.Where(project => project.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return projects.ToArray();
    }
}
