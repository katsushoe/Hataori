using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Hataori.Application;
using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServerProcessCollection
{
    public const string Name = "Real server process";
}

/// <summary>
/// 代用Server（Hataori.Monitor.TestServerStub）を一時インストール先へ「Hataori.Server.exe」として置き、
/// <see cref="UserServerProcess"/> と <see cref="McpServerController"/> を実プロセスで検証します。
/// </summary>
[Collection(ServerProcessCollection.Name)]
public sealed class McpServerProcessTests : IDisposable
{
    private const string StubName = "Hataori.Monitor.TestServerStub";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hataori-server-process-tests", Guid.NewGuid().ToString("N"));
    private readonly InstallationLayout _layout;
    private readonly string _serverExe;
    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false });

    public McpServerProcessTests()
    {
        _layout = new InstallationLayout(_root);
        var serverDirectory = Path.Combine(_layout.BinPath, "server");
        Directory.CreateDirectory(serverDirectory);
        _serverExe = Path.Combine(serverDirectory, "Hataori.Server.exe");
        File.Copy(Path.Combine(AppContext.BaseDirectory, StubName + ".exe"), _serverExe);
        foreach (var suffix in new[] { ".dll", ".runtimeconfig.json", ".deps.json" })
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, StubName + suffix), Path.Combine(serverDirectory, StubName + suffix));
        }

        Environment.SetEnvironmentVariable("HATAORI_STUB_PORT", null);
        Environment.SetEnvironmentVariable("HATAORI_STUB_IGNORE_STDIN", null);
    }

    [Fact]
    public async Task User_server_process_starts_and_stops_gracefully_when_stdin_closes()
    {
        using var host = new UserServerProcess();
        host.Start(_layout);
        Assert.True(host.IsRunning);
        Assert.Single(ServerProcessIds());

        await host.StopAsync();

        Assert.False(host.IsRunning);
        Assert.Empty(ServerProcessIds());
    }

    [Fact]
    public async Task Restart_replaces_the_server_process_with_a_new_one()
    {
        using var host = new UserServerProcess();
        host.Start(_layout);
        var before = Assert.Single(ServerProcessIds());

        await host.RestartAsync();

        Assert.True(host.IsRunning);
        var after = Assert.Single(ServerProcessIds());
        Assert.NotEqual(before, after);
        await host.StopAsync();
    }

    [Fact]
    public async Task Controller_starts_until_healthy_and_stops_to_stopped()
    {
        var port = FreePort();
        Environment.SetEnvironmentVariable("HATAORI_STUB_PORT", port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var host = new UserServerProcess();
        host.Start(_layout);
        await host.StopAsync();
        var controller = Controller(host, port);

        Assert.Equal(McpServerState.Stopped, (await controller.CheckAsync(CancellationToken.None)).State);

        var started = await controller.StartAsync(CancellationToken.None);
        Assert.Equal(McpServerState.Running, started.State);
        Assert.Equal("0.0.0-stub", started.Version);
        Assert.Equal(McpServerState.Running, (await controller.CheckAsync(CancellationToken.None)).State);

        var stopped = await controller.StopAsync(CancellationToken.None);
        Assert.Equal(McpServerState.Stopped, stopped.State);
        Assert.Empty(ServerProcessIds());
    }

    [Fact]
    public async Task Running_process_without_health_response_is_not_responding()
    {
        using var host = new UserServerProcess();
        host.Start(_layout);
        try
        {
            var status = await Controller(host, FreePort()).CheckAsync(CancellationToken.None);
            Assert.Equal(McpServerState.NotResponding, status.State);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Process_with_the_same_name_in_another_installation_is_not_counted()
    {
        var other = new McpServerProcessTests();
        using var otherHost = new UserServerProcess();
        otherHost.Start(other._layout);
        try
        {
            var status = await Controller(new UserServerProcess(), FreePort()).CheckAsync(CancellationToken.None);
            Assert.Equal(McpServerState.Stopped, status.State);
        }
        finally
        {
            await otherHost.StopAsync();
            other.Dispose();
        }
    }

    [Fact]
    public async Task Http_error_from_health_is_error()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            _ = await stream.ReadAsync(new byte[4096]);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"));
        });

        var status = await Controller(new UserServerProcess(), port).CheckAsync(CancellationToken.None);

        Assert.Equal(McpServerState.Error, status.State);
        await serve;
    }

    [Fact]
    public async Task Stop_force_ends_a_server_that_ignores_the_shutdown_request()
    {
        Environment.SetEnvironmentVariable("HATAORI_STUB_IGNORE_STDIN", "1");
        using var host = new UserServerProcess();
        host.Start(_layout);
        Assert.Single(ServerProcessIds());
        var timer = Stopwatch.StartNew();

        var status = await Controller(host, FreePort()).StopAsync(CancellationToken.None);

        Assert.Equal(McpServerState.Stopped, status.State);
        Assert.Empty(ServerProcessIds());
        Assert.True(timer.Elapsed >= TimeSpan.FromSeconds(9), $"forced stop happened after {timer.Elapsed}");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HATAORI_STUB_PORT", null);
        Environment.SetEnvironmentVariable("HATAORI_STUB_IGNORE_STDIN", null);
        foreach (var id in ServerProcessIds())
        {
            try { using var process = Process.GetProcessById(id); process.Kill(entireProcessTree: true); process.WaitForExit(5000); }
            catch (ArgumentException) { }
        }

        _http.Dispose();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private McpServerController Controller(UserServerProcess host, int port) =>
        new(host, _serverExe, new Uri($"http://127.0.0.1:{port}/health"), _http);

    private List<int> ServerProcessIds()
    {
        var ids = new List<int>();
        foreach (var process in Process.GetProcessesByName("Hataori.Server"))
        {
            using (process)
            {
                try
                {
                    if (!process.HasExited && string.Equals(process.MainModule?.FileName, _serverExe, StringComparison.OrdinalIgnoreCase))
                        ids.Add(process.Id);
                }
                catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            }
        }

        return ids;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
