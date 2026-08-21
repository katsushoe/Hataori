using System.Diagnostics;

namespace Hataori.Cli;

/// <summary>
/// Hataori Serverを非対話のバックグラウンドプロセスとして起動します。
/// </summary>
public sealed class ServerProcessLauncher
{
    public ServerProcessResult Start(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var fullPath = Path.GetFullPath(executablePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(Hataori.Application.Localization.DisplayLanguage.Text("Hataori Server実行ファイルが見つかりません。", "Hataori Server executable was not found."), fullPath);
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory,
        }) ?? throw new InvalidOperationException(Hataori.Application.Localization.DisplayLanguage.Text("Hataori Serverを起動できませんでした。", "Hataori Server could not be started."));
        return new ServerProcessResult(process.Id, "starting");
    }
}

/// <summary>
/// Serverプロセス起動結果です。
/// </summary>
public sealed record ServerProcessResult(int ProcessId, string Status);
