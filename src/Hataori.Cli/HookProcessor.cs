using System.Text.Json;
using Hataori.Application.Control;
using Hataori.Core.Tasks;

namespace Hataori.Cli;

/// <summary>Codex／Claude CodeのLifecycle Hook入力をHataori Task Protocolへ接続します。</summary>
public static class HookProcessor
{
    public static object Process(JsonElement input, MonitorSnapshot? snapshot, string? conversationId, string? agentId, string? messageId, string? mcpUrl)
    {
        var eventName = GetString(input, "hook_event_name") ?? GetString(input, "hook_event") ?? throw new ArgumentException("Hook input requires hook_event_name.");
        var tasks = snapshot?.Tasks.Where(task => task.Status == HataoriTaskStatus.Active && Matches(task, conversationId, agentId)).ToArray() ?? [];
        var context = BuildContext(conversationId, agentId, messageId, mcpUrl, tasks);
        return eventName.ToLowerInvariant() switch
        {
            "sessionstart" or "userpromptsubmit" => Context(eventName, context),
            "pretooluse" => PreToolUse(input, tasks),
            "stop" => Stop(input, tasks),
            "sessionend" => new { @continue = true },
            _ => throw new ArgumentException($"Unsupported hook event '{eventName}'."),
        };
    }

    private static object PreToolUse(JsonElement input, IReadOnlyList<HataoriTask> tasks)
    {
        var tool = GetString(input, "tool_name") ?? string.Empty;
        if (!IsMutation(tool, input) || tasks.Count > 0)
        {
            return new { hookSpecificOutput = new { hookEventName = "PreToolUse", additionalContext = tasks.Count > 0 ? $"Active Hataori task: {tasks[0].TaskId}." : "" } };
        }

        return new { hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = "deny", permissionDecisionReason = "変更作業の前にHataoriへTaskを登録してください。" } };
    }

    private static object Stop(JsonElement input, IReadOnlyList<HataoriTask> tasks)
    {
        var alreadyContinued = input.TryGetProperty("stop_hook_active", out var value) && value.ValueKind == JsonValueKind.True;
        return tasks.Count > 0 && !alreadyContinued
            ? new { decision = "block", reason = $"Active Task '{tasks[0].TaskId}'が未完了です。complete、cancel、または必要なheartbeatを実行してください。" }
            : (object)new { @continue = true };
    }

    private static object Context(string eventName, string context) => new { hookSpecificOutput = new { hookEventName = eventName, additionalContext = context } };

    private static bool Matches(HataoriTask task, string? conversationId, string? agentId) =>
        (string.IsNullOrWhiteSpace(conversationId) || string.Equals(task.ConversationId, conversationId, StringComparison.Ordinal)) &&
        (string.IsNullOrWhiteSpace(agentId) || string.Equals(task.AgentId, agentId, StringComparison.OrdinalIgnoreCase));

    private static string BuildContext(string? conversationId, string? agentId, string? messageId, string? mcpUrl, IReadOnlyList<HataoriTask> tasks) =>
        $"Hataori Task Protocol: 変更前にTaskをstartし、進捗をheartbeatし、終了時にcomplete/cancelしてください。 conversation_id={conversationId ?? "(none)"}; agent_id={agentId ?? "(none)"}; origin_message_id={messageId ?? "(none)"}; mcp={mcpUrl ?? "(none)"}; active_tasks={string.Join(',', tasks.Select(task => task.TaskId))}.";

    private static bool IsMutation(string tool, JsonElement input)
    {
        if (tool.Equals("apply_patch", StringComparison.OrdinalIgnoreCase) || tool.Equals("Edit", StringComparison.OrdinalIgnoreCase) || tool.Equals("Write", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!tool.Equals("Bash", StringComparison.OrdinalIgnoreCase) || !input.TryGetProperty("tool_input", out var toolInput) || !toolInput.TryGetProperty("command", out var command))
        {
            return false;
        }

        var text = command.GetString() ?? string.Empty;
        string[] mutationTokens = ["git add", "git commit", "rm ", "mv ", "cp ", "set-content", "remove-item", "move-item", "copy-item", "dotnet format"];
        return mutationTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement input, string name) => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
