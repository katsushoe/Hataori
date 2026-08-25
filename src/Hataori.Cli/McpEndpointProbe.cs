using Hataori.Server;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hataori.Cli;

/// <summary>
/// Hataori MCP Endpointの初期化とTool取得を検証します。
/// </summary>
public sealed class McpEndpointProbe(ILoggerFactory loggerFactory)
{
    /// <summary>
    /// MCP Endpointへ接続し、利用可能なTool件数を返します。
    /// </summary>
    public async Task<object> ProbeAsync(ServerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = BuildEndpoint(options);
        var profile = await ProbeClientAsync(endpoint, "hataori-cli", cancellationToken).ConfigureAwait(false);
        return new { connected = true, endpoint = endpoint.ToString(), tool_count = profile.ToolCount };
    }

    /// <summary>
    /// CodexとClaude CodeのClient情報で同一のTool契約と代表的な応答を取得できることを検証します。
    /// </summary>
    public async Task<object> ProbeCompatibilityAsync(ServerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = BuildEndpoint(options);
        var codex = await ProbeClientAsync(endpoint, "codex", cancellationToken).ConfigureAwait(false);
        var claudeCode = await ProbeClientAsync(endpoint, "claude-code", cancellationToken).ConfigureAwait(false);
        var compatible = AreEquivalent(codex, claudeCode);
        return new { compatible, endpoint = endpoint.ToString(), clients = new[] { codex, claudeCode } };
    }

    /// <summary>2つのClient検証結果が同一のMCP契約を示すか判定します。</summary>
    public static bool AreEquivalent(McpClientProfileResult first, McpClientProfileResult second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return first.ToolNames.SequenceEqual(second.ToolNames, StringComparer.Ordinal)
            && string.Equals(first.ContractHash, second.ContractHash, StringComparison.Ordinal)
            && string.Equals(first.VersionResult, second.VersionResult, StringComparison.Ordinal);
    }

    private async Task<McpClientProfileResult> ProbeClientAsync(Uri endpoint, string clientName, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = "hataori",
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            EnableStandaloneGetStream = false,
        }, loggerFactory);
        var clientOptions = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = clientName, Version = "compatibility-probe" },
        };
        await using var client = await McpClient.CreateAsync(transport, clientOptions, loggerFactory, cancellationToken).ConfigureAwait(false);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var toolNames = tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray();
        var contract = JsonSerializer.Serialize(tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).Select(tool => tool.ProtocolTool));
        var contractHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contract)));
        var version = await client.CallToolAsync("get_version", cancellationToken: cancellationToken).ConfigureAwait(false);
        return new McpClientProfileResult(clientName, tools.Count, toolNames, contractHash, JsonSerializer.Serialize(version.StructuredContent));
    }

    /// <summary>
    /// Server設定からMCP Endpoint URIを構築します。
    /// </summary>
    public static Uri BuildEndpoint(ServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new UriBuilder(Uri.UriSchemeHttp, options.McpHost, options.McpPort, options.McpPath).Uri;
    }
}

/// <summary>MCP Client別の互換性検証結果です。</summary>
public sealed record McpClientProfileResult(string Client, int ToolCount, IReadOnlyList<string> ToolNames, string ContractHash, string VersionResult);
