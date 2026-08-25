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

    [Fact]
    public void AreEquivalent_SameToolsAndResult_ReturnsTrue()
    {
        var codex = new McpClientProfileResult("codex", 2, ["get_version", "task_list"], "ABC", "{\"version\":\"1.0.0.0\"}");
        var claudeCode = new McpClientProfileResult("claude-code", 2, ["get_version", "task_list"], "ABC", "{\"version\":\"1.0.0.0\"}");

        McpEndpointProbe.AreEquivalent(codex, claudeCode).Should().BeTrue();
    }

    [Fact]
    public void AreEquivalent_DifferentToolContract_ReturnsFalse()
    {
        var codex = new McpClientProfileResult("codex", 2, ["get_version", "task_list"], "ABC", "{\"version\":\"1.0.0.0\"}");
        var claudeCode = new McpClientProfileResult("claude-code", 1, ["get_version"], "DEF", "{\"version\":\"1.0.0.0\"}");

        McpEndpointProbe.AreEquivalent(codex, claudeCode).Should().BeFalse();
    }

    [Fact]
    public void AreEquivalent_DifferentSchemaHash_ReturnsFalse()
    {
        var codex = new McpClientProfileResult("codex", 1, ["task_list"], "ABC", "{\"version\":\"1.0.0.0\"}");
        var claudeCode = new McpClientProfileResult("claude-code", 1, ["task_list"], "DEF", "{\"version\":\"1.0.0.0\"}");

        McpEndpointProbe.AreEquivalent(codex, claudeCode).Should().BeFalse();
    }
}
