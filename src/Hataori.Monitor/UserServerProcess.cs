using System.Diagnostics;
using Hataori.Application;

namespace Hataori.Monitor;

/// <summary>同じユーザーで起動したServerを、Monitorの稼働期間に合わせて停止します。</summary>
internal sealed class UserServerProcess : IDisposable
{
    private Process? _process;

    public bool HasExited => _process?.HasExited == true;
    public int? ExitCode => HasExited ? _process?.ExitCode : null;

    public void Start(InstallationLayout layout)
    {
        if (_process is { HasExited: false }) return;
        _process?.Dispose();
        var executable = Path.Combine(layout.BinPath, "server", "Hataori.Server.exe");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            WorkingDirectory = layout.RootPath,
        };
        start.ArgumentList.Add("--monitor-parent");
        _process = Process.Start(start) ?? throw new IOException("Could not start Hataori Server.");
    }

    public async Task StopAsync()
    {
        if (_process is null || _process.HasExited) return;
        _process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_process is { HasExited: false }) _process.StandardInput.Close();
        _process?.Dispose();
    }
}
