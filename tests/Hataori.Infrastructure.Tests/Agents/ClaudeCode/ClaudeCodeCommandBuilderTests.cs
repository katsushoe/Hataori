using FluentAssertions;
using Hataori.Infrastructure.Agents.ClaudeCode;

namespace Hataori.Infrastructure.Tests.Agents.ClaudeCode;

public sealed class ClaudeCodeCommandBuilderTests
{
    [Fact]
    public void BuildStart_UsesPrintJsonAndSafePermissionMode()
    {
        var options = new ClaudeCodeDriverOptions { PermissionMode = "acceptEdits", Model = "test-model" };

        var arguments = ClaudeCodeCommandBuilder.BuildStart(options);

        arguments.Should().Equal("-p", "--output-format", "json", "--permission-mode", "acceptEdits", "--model", "test-model");
    }

    [Fact]
    public void BuildResume_UsesExplicitSession()
    {
        var arguments = ClaudeCodeCommandBuilder.BuildResume(new ClaudeCodeDriverOptions(), "session-1");

        arguments.Should().Equal("-p", "--output-format", "json", "--permission-mode", "acceptEdits", "--resume", "session-1");
    }
}
