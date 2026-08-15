using FluentAssertions;
using Hataori.Infrastructure.Agents.Codex;

namespace Hataori.Server.Tests;

public sealed class CodexDriverOptionsValidatorTests
{
    [Fact]
    public void Validate_WorkspaceWriteConfiguration_ReturnsSuccess()
    {
        var result = new CodexDriverOptionsValidator().Validate(null, new CodexDriverOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_DangerFullAccess_ReturnsFailure()
    {
        var result = new CodexDriverOptionsValidator().Validate(null, new CodexDriverOptions { SandboxMode = "danger-full-access" });

        result.Failed.Should().BeTrue();
    }
}
