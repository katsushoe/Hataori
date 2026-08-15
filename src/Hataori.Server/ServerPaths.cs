namespace Hataori.Server;

/// <summary>
/// Serverで使用するパスを実行環境から解決します。
/// </summary>
public static class ServerPaths
{
    public static string ResolveDatabasePath(string configuredPath, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        return Path.GetFullPath(configuredPath, baseDirectory);
    }
}
