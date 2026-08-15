namespace Hataori.Infrastructure.Agents.Codex;

public sealed class CodexDriverOptions
{
    public const string SectionName = "agents:codex";

    public string ExecutablePath { get; init; } = "codex";
    public string SandboxMode { get; init; } = "workspace-write";
    public bool ApproveForMe { get; init; } = true;
    public string? Model { get; init; }
    public int MaxCapturedCharacters { get; init; } = 4 * 1024 * 1024;
}
