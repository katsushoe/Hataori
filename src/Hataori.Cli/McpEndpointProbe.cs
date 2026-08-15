using Hataori.Server;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

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
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = "hataori",
            Endpoint = endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10),
            EnableStandaloneGetStream = false,
        }, loggerFactory);
        await using var client = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken).ConfigureAwait(false);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return new { connected = true, endpoint = endpoint.ToString(), tool_count = tools.Count };
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
