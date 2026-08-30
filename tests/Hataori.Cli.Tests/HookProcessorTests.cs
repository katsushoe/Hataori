using System.Text.Json;
using FluentAssertions;
using Hataori.Application.Control;

namespace Hataori.Cli.Tests;

public sealed class HookProcessorTests
{
    [Fact]
    public void Process_PreToolUseWithoutTask_DeniesMutationAndFlagsPermissionDenied()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","tool_name":"apply_patch","tool_input":{"command":"patch"}}""");

        var result = HookProcessor.Process(input.RootElement, Snapshot([]), "conversation", "codex", null, "http://localhost/mcp");

        JsonSerializer.Serialize(result.Payload).Should().Contain("deny").And.Contain("Task");
        result.PermissionDenied.Should().BeTrue();
        result.DenialReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Process_UserPromptSubmit_PromptsProjectLookupBeforeTaskRegistration()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"UserPromptSubmit"}""");

        var result = HookProcessor.Process(input.RootElement, Snapshot([]), "conversation", "codex", null, "http://localhost/mcp");

        JsonSerializer.Serialize(result.Payload).Should().Contain("list_workspaces").And.Contain("list_projects");
    }

    [Fact]
    public void Process_PreToolUseWithActiveTask_AllowsMutationAndDoesNotFlagPermissionDenied()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","tool_name":"apply_patch","tool_input":{"command":"patch"}}""");
        var task = Task("task-1");

        var result = HookProcessor.Process(input.RootElement, Snapshot([task]), "conversation", "codex", null, null);

        JsonSerializer.Serialize(result.Payload).Should().NotContain("deny").And.Contain("task-1");
        result.PermissionDenied.Should().BeFalse();
        result.DenialReason.Should().BeNull();
    }

    [Fact]
    public void Process_PreToolUseNonMutatingTool_DoesNotFlagPermissionDenied()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","tool_name":"Read","tool_input":{}}""");

        var result = HookProcessor.Process(input.RootElement, Snapshot([]), "conversation", "codex", null, null);

        result.PermissionDenied.Should().BeFalse();
    }

    [Fact]
    public void Process_Stop_BlocksOnlyFirstContinuation()
    {
        var task = Task("task-1");
        using var first = JsonDocument.Parse("""{"hook_event_name":"Stop","stop_hook_active":false}""");
        using var continued = JsonDocument.Parse("""{"hook_event_name":"Stop","stop_hook_active":true}""");

        var firstResult = HookProcessor.Process(first.RootElement, Snapshot([task]), "conversation", "codex", null, null);
        var continuedResult = HookProcessor.Process(continued.RootElement, Snapshot([task]), "conversation", "codex", null, null);

        JsonSerializer.Serialize(firstResult.Payload).Should().Contain("block");
        firstResult.PermissionDenied.Should().BeFalse();
        JsonSerializer.Serialize(continuedResult.Payload).Should().Contain("continue");
    }

    private static MonitorSnapshot Snapshot(IReadOnlyList<MonitorTask> tasks) => new(tasks, [], [], [], 0, new MonitorSystemStatus("running", "connected", "running", "connected"));

    private static MonitorTask Task(string taskId) => new("default", taskId, "Hook", "codex", "conversation", "active", "work", 0, DateTimeOffset.UtcNow);
}
