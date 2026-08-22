using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>起動Provider優先順位を管理するMCP Toolsです。</summary>
[McpServerToolType]
public sealed class ProviderMcpTools(ProviderPriorityService service)
{
    [McpServerTool(Name = "get_provider_priority", ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets the provider search priority used for automatic agent activation.")]
    public Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken)
        => service.GetAsync(cancellationToken);

    [McpServerTool(Name = "set_provider_priority", Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Sets the provider search priority used for automatic agent activation.")]
    public Task<IReadOnlyList<string>> SetAsync(
        [Description("Provider IDs in highest-to-lowest priority order.")] IReadOnlyList<string> providers,
        CancellationToken cancellationToken)
        => service.SetAsync(providers, cancellationToken);
}
