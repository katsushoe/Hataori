using FluentAssertions;
using Hataori.Infrastructure.Agents.Codex;

namespace Hataori.Infrastructure.Tests.Agents.Codex;

public sealed class CodexCommandBuilderTests
{
    [Fact]
    public void BuildStart_UsesJsonWorkspaceSandboxAndStdin()
    {
        var options = new CodexDriverOptions { SandboxMode = "workspace-write", ApproveForMe = true, Model = "test-model" };

        var arguments = CodexCommandBuilder.BuildStart(options, "C:\\workspace");

        arguments.Should().Equal("exec", "--json", "--color", "never", "--approve-for-me", "--model", "test-model", "--cd", "C:\\workspace", "-");
    }

    [Fact]
    public void BuildStart_WithoutAutomaticApproval_UsesConfiguredSandbox()
    {
        var options = new CodexDriverOptions { SandboxMode = "read-only", ApproveForMe = false };

        var arguments = CodexCommandBuilder.BuildStart(options, "C:\\workspace");

        arguments.Should().ContainInOrder("--sandbox", "read-only");
        arguments.Should().NotContain("--approve-for-me");
    }

    [Fact]
    public void BuildResume_UsesExplicitSessionAndStdin()
    {
        var arguments = CodexCommandBuilder.BuildResume(new CodexDriverOptions(), "session-1");

        arguments.Should().Equal("exec", "resume", "--json", "session-1", "-");
    }
}
