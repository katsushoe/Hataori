using FluentAssertions;
using Hataori.Infrastructure.Agents.ClaudeCode;

namespace Hataori.Server.Tests;

public sealed class ClaudeCodeDriverOptionsValidatorTests
{
    [Fact]
    public void Validate_AcceptEdits_ReturnsSuccess()
    {
        new ClaudeCodeDriverOptionsValidator().Validate(null, new ClaudeCodeDriverOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_BypassPermissions_ReturnsFailure()
    {
        var options = new ClaudeCodeDriverOptions { PermissionMode = "bypassPermissions" };

        new ClaudeCodeDriverOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }
}
