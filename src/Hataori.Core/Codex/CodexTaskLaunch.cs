namespace Hataori.Core.Codex;

/// <summary>Codex Desktop受信タスクが処理するタスク起動要求です。</summary>
public sealed record CodexTaskLaunch(
    string MessageId,
    string ClaimToken,
    string ProjectName,
    string WorkingDirectory,
    string Prompt,
    string ConversationId,
    string SenderAgentId,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset LeaseUntilUtc);
