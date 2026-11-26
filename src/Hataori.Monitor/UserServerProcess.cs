using System.Diagnostics;
using Hataori.Application;

namespace Hataori.Monitor;

/// <summary>Monitorが所有するServerの停止・再起動操作です。</summary>
internal interface IMonitorServerHost
{
    bool HasExited { get; }

    Task StopAsync();

    Task RestartAsync();
}

/// <summary>同じユーザーで起動したServerを、Monitorの稼働期間に合わせて停止します。</summary>
internal sealed class UserServerProcess : IMonitorServerHost, IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(25);
    private Process? _process;
    private InstallationLayout? _layout;

    public bool HasExited => _process?.HasExited == true;
    public int? ExitCode => HasExited ? _process?.ExitCode : null;

    public void Start(InstallationLayout layout)
    {
        _layout = layout;
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
        using var timeout = new CancellationTokenSource(StopTimeout);
        try
        {
            await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"Hataori Server did not stop within {StopTimeout.TotalSeconds:0} seconds.");
        }
    }

    /// <summary>Serverを正常停止してから同じ配置で起動し直します。停止できない場合は起動しません。</summary>
    public async Task RestartAsync()
    {
        var layout = _layout ?? throw new InvalidOperationException("Hataori Server has not been started by Monitor.");
        await StopAsync().ConfigureAwait(false);
        Start(layout);
    }

    public void Dispose()
    {
        if (_process is { HasExited: false }) _process.StandardInput.Close();
        _process?.Dispose();
    }
}
