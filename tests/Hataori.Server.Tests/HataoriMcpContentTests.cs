using FluentAssertions;
using Hataori.Server;

namespace Hataori.Server.Tests;

public sealed class HataoriMcpContentTests
{
    [Fact]
    public void Instructions_常時_Hataoriの目的と標準Task手順を含む()
    {
        HataoriMcpInstructions.Text.Should().Contain("coordinates work");
        HataoriMcpInstructions.Text.Should().Contain("list_projects");
        HataoriMcpInstructions.Text.Should().Contain("task_find_conflicts");
        HataoriMcpInstructions.Text.Should().Contain("task_heartbeat");
        HataoriMcpInstructions.Text.Should().Contain("Itoguruma");
    }

    [Fact]
    public void Workflow_作業内容指定_作業内容と完了までの手順を返す()
    {
        var result = HataoriMcpPrompts.Workflow("Server Instructionsを実装する");

        result.Should().Contain("Server Instructionsを実装する");
        result.Should().Contain("list_projects");
        result.Should().Contain("start one task");
        result.Should().Contain("explicit progress percentage");
        result.Should().Contain("Complete the task");
    }

    [Fact]
    public void Workflow_空の作業内容_例外を返す()
    {
        var action = () => HataoriMcpPrompts.Workflow(" ");

        action.Should().Throw<ArgumentException>();
    }
}
