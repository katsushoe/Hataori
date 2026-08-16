namespace Hataori.Application;

/// <summary>Hataoriの標準インストールディレクトリを解決します。</summary>
public sealed record InstallationLayout(string RootPath)
{
    /// <summary>バイナリディレクトリです。</summary>
    public string BinPath => Path.Combine(RootPath, "bin");

    /// <summary>設定ディレクトリです。</summary>
    public string ConfigPath => Path.Combine(RootPath, "config");

    /// <summary>ログディレクトリです。</summary>
    public string LogsPath => Path.Combine(RootPath, "logs");

    /// <summary>データディレクトリです。</summary>
    public string DataPath => Path.Combine(RootPath, "data");

    /// <summary>標準設定ファイルです。</summary>
    public string ConfigurationPath => Path.Combine(ConfigPath, "hataori.json");

    /// <summary>実行位置からインストールルートを解決します。</summary>
    public static InstallationLayout Resolve(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        var fullPath = Path.GetFullPath(baseDirectory);
        for (var directory = new DirectoryInfo(fullPath); directory is not null; directory = directory.Parent)
        {
            if (!directory.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) || directory.Parent is null)
            {
                continue;
            }

            var root = directory.Parent.FullName;
            if (Directory.Exists(Path.Combine(root, "config")) &&
                Directory.Exists(Path.Combine(root, "logs")) &&
                Directory.Exists(Path.Combine(root, "data")))
            {
                return new InstallationLayout(root);
            }
        }

        return new InstallationLayout(fullPath);
    }

    /// <summary>可変データ用の標準ディレクトリを作成します。</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(ConfigPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(DataPath);
    }
}
