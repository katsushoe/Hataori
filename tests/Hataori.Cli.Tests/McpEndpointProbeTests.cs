using FluentAssertions;
using Hataori.Server;

namespace Hataori.Cli.Tests;

public sealed class McpEndpointProbeTests
{
    [Fact]
    public void BuildEndpoint_ServerOptions_ReturnsConfiguredUri()
    {
        var options = new ServerOptions
        {
            McpHost = "127.0.0.1",
            McpPort = 45440,
            McpPath = "/mcp",
        };

        var endpoint = McpEndpointProbe.BuildEndpoint(options);

        endpoint.Should().Be(new Uri("http://127.0.0.1:45440/mcp"));
    }
}
