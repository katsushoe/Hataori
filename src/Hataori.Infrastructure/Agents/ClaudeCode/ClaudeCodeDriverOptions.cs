namespace Hataori.Infrastructure.Agents.ClaudeCode;

public sealed class ClaudeCodeDriverOptions
{
    public const string SectionName = "agents:claudeCode";

    public string ExecutablePath { get; init; } = "claude";
    public string PermissionMode { get; init; } = "acceptEdits";
    public string? Model { get; init; }
    public int MaxCapturedCharacters { get; init; } = 4 * 1024 * 1024;
}
