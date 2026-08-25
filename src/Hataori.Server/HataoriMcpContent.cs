using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Hataori.Server;

/// <summary>Hataori MCP Clientへ公開するServer Instructionsです。</summary>
public static class HataoriMcpInstructions
{
    /// <summary>Hataoriの目的と基本的な利用手順を示します。</summary>
    public const string Text = """
        Hataori coordinates work performed by Codex Desktop, Codex CLI, and Claude Code on this Windows machine. It tracks tasks, progress, relationships, conversation sessions, messages, and agent runs so agents can avoid duplicate work and leave an auditable history.

        Before implementation work, call task_list and task_find_conflicts, then call task_start with a stable task ID. While working, call task_heartbeat with an explicit progress percentage. Finish with task_complete, task_fail, or task_cancel. Use task_get, task_history, and task_relations to inspect state. Use agent-run and provider-priority tools only when agent execution or provider selection is part of the requested work.

        Hataori is the task and agent-run coordinator. It does not replace project-specific instructions, source control procedures, tests, or direct project-to-project communication through Itoguruma. Do not place Itoguruma credentials in Hataori MCP requests or client settings.

        Use the hataori_workflow prompt when a reusable step-by-step operating guide is helpful.
        """;
}

/// <summary>Hataoriの標準利用手順を提供するMCP Promptsです。</summary>
public sealed class HataoriMcpPrompts
{
    /// <summary>依頼内容に合わせたHataori利用手順を返します。</summary>
    [McpServerPrompt(Name = "hataori_workflow", Title = "Hataori work coordination workflow")]
    [Description("Creates a concise workflow for coordinating a task with Hataori, including conflict checks, progress reporting, and completion.")]
    public static string Workflow(
        [Description("A short description of the work to coordinate.")] string work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(work);

        return $"""
            Coordinate this work with Hataori: {work}

            1. Read the active task list and check for conflicting work before changing files.
            2. If there is no conflict, start one task with a stable task ID, clear summary, current work, and agent identity.
            3. Follow the repository's own instructions and keep changes within the requested project scope.
            4. Send task heartbeats during meaningful progress updates; always include an explicit progress percentage from 0 through 100.
            5. Inspect task history or relationships when prior work or dependencies matter.
            6. Complete the task only after required verification succeeds. Otherwise record failure or cancellation with an honest result.

            Hataori records and coordinates the work. Use Itoguruma directly for project-to-project questions, notifications, progress checks, and change requests.
            """;
    }
}
