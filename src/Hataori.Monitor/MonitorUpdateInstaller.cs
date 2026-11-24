using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Hataori.Application;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

internal sealed record MonitorUpdateJob(int ParentId, string PackagePath, string Sha256, string MonitorPath, string InstallRoot, string PipeName, string Language, bool StartInTray = false);

/// <summary>Monitor終了後にMSIを実行し、更新後または失敗時にMonitorを再開します。</summary>
internal static class MonitorUpdateInstaller
{
    public static void Start(string packagePath, MonitorRelease release, string pipeName, string language, bool startInTray)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Monitor executable path is unavailable.");
        var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
        if (!IsInstalledMonitor(AppContext.BaseDirectory, layout.RootPath))
            throw new InvalidOperationException("Automatic installation requires an MSI installation. Install the MSI before using automatic updates.");
        // MSIが更新する実行ファイルを保持しないよう、専用ディレクトリのコピーから実行します。
        var directory = Path.GetDirectoryName(packagePath)!;
        var helperDirectory = Path.Combine(directory, "helper");
        Directory.CreateDirectory(helperDirectory);
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            File.Copy(file, Path.Combine(helperDirectory, Path.GetFileName(file)));
        var job = new MonitorUpdateJob(Environment.ProcessId, packagePath, release.Sha256, executable,
            layout.RootPath, pipeName, language, startInTray);
        var jobPath = Path.Combine(directory, "job.json");
        File.WriteAllText(jobPath, JsonSerializer.Serialize(job));
        var start = new ProcessStartInfo(Path.Combine(helperDirectory, Path.GetFileName(executable)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = helperDirectory,
        };
        start.ArgumentList.Add("--apply-update");
        start.ArgumentList.Add(jobPath);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the update coordinator.");
    }

    internal static bool IsInstalledMonitor(string baseDirectory, string installRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)).Equals(
            Path.Combine(Path.GetFullPath(installRoot), "bin", "monitor"), StringComparison.OrdinalIgnoreCase)
        && File.Exists(Path.Combine(installRoot, "config", "hataori.json"));

    public static async Task<int> ApplyAsync(string jobPath, MonitorErrorHandler errors)
    {
        MonitorUpdateJob? job = null;
        var installerRunning = false;
        try
        {
            job = JsonSerializer.Deserialize<MonitorUpdateJob>(await File.ReadAllTextAsync(jobPath).ConfigureAwait(false))
                ?? throw new InvalidDataException("Invalid update job.");
            var culture = CultureInfo.GetCultureInfo(job.Language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(20));
            await WaitForParentAsync(job.ParentId, deadline.Token).ConfigureAwait(false);
            await using var package = new FileStream(job.PackagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(package, deadline.Token).ConfigureAwait(false));
            if (!digest.Equals(job.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update SHA-256 mismatch.");
            var start = CreateInstallerStart(job);
            using var installer = Process.Start(start) ?? throw new IOException("Windows Installer did not start.");
            installerRunning = true;
            await installer.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            installerRunning = false;
            if (installer.ExitCode is not (0 or 3010)) throw new IOException($"Windows Installer exit code: {installer.ExitCode}.");
            if (installer.ExitCode == 3010)
                MessageBox.Show(DisplayLanguage.Text("更新が完了しました。Windowsを再起動してください。", "Update complete. Restart Windows."), "Hataori Monitor");
            return 0;
        }
        catch (Exception exception)
        {
            errors.Report(exception, null, DisplayLanguage.Text("自動更新を完了できませんでした。現在のバージョンを使用できます。Windows Installerが動作中の場合は完了を待ってください。", "The update did not complete. You can use the current version. If Windows Installer is still running, wait for it to finish."), showDialog: true);
            return 1;
        }
        finally
        {
            if (job is not null && !installerRunning)
            {
                try
                {
                    var restart = new ProcessStartInfo(job.MonitorPath) { UseShellExecute = false };
                    restart.ArgumentList.Add("--pipe");
                    restart.ArgumentList.Add(job.PipeName);
                    restart.ArgumentList.Add("--skip-update");
                    if (job.StartInTray) restart.ArgumentList.Add("--startup");
                    using var process = Process.Start(restart) ?? throw new IOException("Could not restart Monitor.");
                }
                catch (Exception exception)
                {
                    errors.Report(exception, null, DisplayLanguage.Text("Monitorを再起動できませんでした。スタートメニューからHataori Monitorを起動してください。", "Could not restart Monitor. Open Hataori Monitor from the Start menu."), showDialog: true);
                }
            }
        }
    }

    internal static ProcessStartInfo CreateInstallerStart(MonitorUpdateJob job)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("/i");
        start.ArgumentList.Add(job.PackagePath);
        start.ArgumentList.Add("/passive");
        start.ArgumentList.Add("/norestart");
        start.ArgumentList.Add("INSTALL_ROOT=" + job.InstallRoot);
        start.ArgumentList.Add("/L*v");
        start.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(job.PackagePath)!, "install.log"));
        return start;
    }

    private static async Task WaitForParentAsync(int parentId, CancellationToken cancellationToken)
    {
        Process parent;
        try { parent = Process.GetProcessById(parentId); }
        catch (ArgumentException) { return; }
        using (parent)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            await parent.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
    }
}
