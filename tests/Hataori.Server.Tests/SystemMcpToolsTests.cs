using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class SystemMcpToolsTests
{
    [Fact]
    public void GetVersion_ReturnsHataoriNameAndAssemblyVersion()
    {
        var tools = new SystemMcpTools();

        var result = tools.GetVersion();

        result.Name.Should().Be("Hataori");
        result.Version.Should().Be(typeof(SystemMcpTools).Assembly.GetName().Version?.ToString());
    }
}
