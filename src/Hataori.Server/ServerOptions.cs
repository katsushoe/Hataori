namespace Hataori.Server;

/// <summary>
/// Hataori Serverの実行設定です。
/// </summary>
public sealed class ServerOptions
{
    public const string SectionName = "server";

    public string DatabasePath { get; init; } = string.Empty;
    public string ControlPipeName { get; init; } = string.Empty;
    public string McpHost { get; init; } = string.Empty;
    public int McpPort { get; init; }
    public string McpPath { get; init; } = string.Empty;
}
