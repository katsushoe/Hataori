using FluentAssertions;
using Hataori.Core.Workspaces;

namespace Hataori.Core.Tests.Workspaces;

public sealed class WorkspaceIdTests
{
    [Theory]
    [InlineData(null, "default")]
    [InlineData(" Main1 ", "main1")]
    public void Normalize_ValidValue_ReturnsCanonicalId(string? value, string expected)
    {
        WorkspaceId.Normalize(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("1main")]
    [InlineData("main-workspace")]
    [InlineData("main_workspace")]
    public void Normalize_InvalidValue_Throws(string value)
    {
        var action = () => WorkspaceId.Normalize(value);

        action.Should().Throw<ArgumentException>();
    }
}
