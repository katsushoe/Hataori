using System.Text.Json;
using Hataori.Application.Control;

namespace Hataori.Cli;

/// <summary>Hook処理の結果です。Payloadが実際にstdoutへ書き出すJSON、PermissionDeniedはPreToolUseがtool呼び出しを拒否したかを示します。</summary>
public sealed record HookResult(object Payload, bool PermissionDenied, string? DenialReason);

/// <summary>Codex／Claude CodeのLifecycle Hook入力をHataori Task Protocolへ接続します。</summary>
public static class HookProcessor
{
    public static HookResult Process(JsonElement input, MonitorSnapshot? snapshot, string? conversationId, string? agentId, string? messageId, string? mcpUrl)
    {
        var eventName = GetString(input, "hook_event_name") ?? GetString(input, "hook_event") ?? throw new ArgumentException(Hataori.Application.Localization.DisplayLanguage.Text("Hook入力にはhook_event_nameが必要です。", "Hook input requires hook_event_name."));
        var tasks = snapshot?.Tasks.Where(task => string.Equals(task.Status, "active", StringComparison.OrdinalIgnoreCase) && Matches(task, conversationId, agentId)).ToArray() ?? [];
        var context = BuildContext(conversationId, agentId, messageId, mcpUrl, tasks);
        return eventName.ToLowerInvariant() switch
        {
            "sessionstart" or "userpromptsubmit" => new HookResult(Context(eventName, context), false, null),
            "pretooluse" => PreToolUse(input, tasks),
            "stop" => new HookResult(Stop(input, tasks), false, null),
            "sessionend" => new HookResult(new { @continue = true }, false, null),
            _ => throw new ArgumentException(Hataori.Application.Localization.DisplayLanguage.Text($"未対応のHook eventです: '{eventName}'。", $"Unsupported hook event '{eventName}'.")),
        };
    }

    private static HookResult PreToolUse(JsonElement input, IReadOnlyList<MonitorTask> tasks)
    {
        var tool = GetString(input, "tool_name") ?? string.Empty;
        if (!IsMutation(tool, input) || tasks.Count > 0)
        {
            var payload = new { hookSpecificOutput = new { hookEventName = "PreToolUse", additionalContext = tasks.Count > 0 ? $"Active Hataori task: {tasks[0].TaskId}." : "" } };
            return new HookResult(payload, false, null);
        }

        const string reason = "変更作業の前にHataoriへTaskを登録してください。";
        return new HookResult(new { hookSpecificOutput = new { hookEventName = "PreToolUse", permissionDecision = "deny", permissionDecisionReason = reason } }, true, reason);
    }

    private static object Stop(JsonElement input, IReadOnlyList<MonitorTask> tasks)
    {
        var alreadyContinued = input.TryGetProperty("stop_hook_active", out var value) && value.ValueKind == JsonValueKind.True;
        return tasks.Count > 0 && !alreadyContinued
            ? new { decision = "block", reason = $"Active Task '{tasks[0].TaskId}'が未完了です。complete、cancel、または必要なheartbeatを実行してください。" }
            : (object)new { @continue = true };
    }

    private static object Context(string eventName, string context) => new { hookSpecificOutput = new { hookEventName = eventName, additionalContext = context } };

    private static bool Matches(MonitorTask task, string? conversationId, string? agentId) =>
        (string.IsNullOrWhiteSpace(conversationId) || string.Equals(task.ConversationId, conversationId, StringComparison.Ordinal)) &&
        (string.IsNullOrWhiteSpace(agentId) || string.Equals(task.AgentId, agentId, StringComparison.OrdinalIgnoreCase));

    private static string BuildContext(string? conversationId, string? agentId, string? messageId, string? mcpUrl, IReadOnlyList<MonitorTask> tasks) =>
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
