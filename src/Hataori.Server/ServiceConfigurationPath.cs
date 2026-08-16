namespace Hataori.Server;

/// <summary>Windows Service専用設定ファイルの場所を解決します。</summary>
public static class ServiceConfigurationPath
{
    /// <summary>標準設定ディレクトリ内のサービス専用設定ファイルを返します。</summary>
    public static string GetDefaultPath(string? baseDirectory = null) => Path.Combine(
        Hataori.Application.InstallationLayout.Resolve(baseDirectory ?? AppContext.BaseDirectory).ConfigPath,
        "hataori.service.json");
}
