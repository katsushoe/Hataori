using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>
/// AI Agentへ公開するSystem向けツールです。
/// </summary>
[McpServerToolType]
public sealed class SystemMcpTools
{
    [McpServerTool(Name = "get_version", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the running Hataori server name and version.")]
    public HataoriVersionInfo GetVersion()
        => new("Hataori", typeof(SystemMcpTools).Assembly.GetName().Version?.ToString() ?? "unknown");
}

public sealed record HataoriVersionInfo(string Name, string Version);
