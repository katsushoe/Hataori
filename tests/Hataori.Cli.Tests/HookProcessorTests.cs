using System.Text.Json;
using FluentAssertions;
using Hataori.Application.Control;
using Hataori.Core.Tasks;

namespace Hataori.Cli.Tests;

public sealed class HookProcessorTests
{
    [Fact]
    public void Process_PreToolUseWithoutTask_DeniesMutation()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","tool_name":"apply_patch","tool_input":{"command":"patch"}}""");

        var result = HookProcessor.Process(input.RootElement, Snapshot([]), "conversation", "codex", null, "http://localhost/mcp");

        JsonSerializer.Serialize(result).Should().Contain("deny").And.Contain("Task");
    }

    [Fact]
    public void Process_PreToolUseWithActiveTask_AllowsMutation()
    {
        using var input = JsonDocument.Parse("""{"hook_event_name":"PreToolUse","tool_name":"apply_patch","tool_input":{"command":"patch"}}""");
        var task = HataoriTask.Start("task-1", "Hook", "codex", "conversation", null, "summary", "work", DateTimeOffset.UtcNow);

        var result = HookProcessor.Process(input.RootElement, Snapshot([task]), "conversation", "codex", null, null);

        JsonSerializer.Serialize(result).Should().NotContain("deny").And.Contain("task-1");
    }

    [Fact]
    public void Process_Stop_BlocksOnlyFirstContinuation()
    {
        var task = HataoriTask.Start("task-1", "Hook", "codex", "conversation", null, "summary", "work", DateTimeOffset.UtcNow);
        using var first = JsonDocument.Parse("""{"hook_event_name":"Stop","stop_hook_active":false}""");
        using var continued = JsonDocument.Parse("""{"hook_event_name":"Stop","stop_hook_active":true}""");

        JsonSerializer.Serialize(HookProcessor.Process(first.RootElement, Snapshot([task]), "conversation", "codex", null, null)).Should().Contain("block");
        JsonSerializer.Serialize(HookProcessor.Process(continued.RootElement, Snapshot([task]), "conversation", "codex", null, null)).Should().Contain("continue");
    }

    private static MonitorSnapshot Snapshot(IReadOnlyList<HataoriTask> tasks) => new(tasks, [], [], [], 0, new MonitorSystemStatus("running", "connected", "running", "connected"));
}
