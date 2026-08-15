namespace Hataori.Server;

/// <summary>
/// Hataori Serverのファイルログ設定です。
/// </summary>
public sealed class FileLogOptions
{
    public const string SectionName = "fileLogging";

    public bool Enabled { get; init; } = true;
    public string DirectoryPath { get; init; } = "logs";
    public string MinimumLevel { get; init; } = "Information";
    public int RetentionDays { get; init; } = 30;
}
