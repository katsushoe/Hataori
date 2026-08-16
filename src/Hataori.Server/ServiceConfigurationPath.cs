namespace Hataori.Server;

/// <summary>Windows Service専用設定ファイルの場所を解決します。</summary>
public static class ServiceConfigurationPath
{
    /// <summary>ProgramData配下のサービス専用設定ファイルを返します。</summary>
    public static string GetDefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Hataori",
        "hataori.service.json");
}
