namespace Hataori.Server;

/// <summary>Agent Lifecycle Hook設定です。</summary>
public sealed class HookOptions
{
    public const string SectionName = "hooks";
    public bool Enabled { get; init; } = true;
    public string CodexConfigPath { get; init; } = string.Empty;
    public string ClaudeConfigPath { get; init; } = string.Empty;
}
