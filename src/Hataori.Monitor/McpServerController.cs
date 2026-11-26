using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Hataori.Monitor;

/// <summary>トレイに表示するMCPサーバーの状態です。</summary>
public enum McpServerState
{
    Unknown,
    Running,
    NotResponding,
    Error,
    Stopped,
}

/// <summary>MCPサーバーの状態と、稼働中の場合の版です。</summary>
internal sealed record McpServerStatus(McpServerState State, string? Version = null)
{
    public static McpServerStatus Unknown { get; } = new(McpServerState.Unknown);
}

/// <summary>トレイメニューから使うMCPサーバーの状態確認・起動・停止操作です。</summary>
internal interface IMcpServerController
{
    /// <summary>Monitorがこのインストール先のServerを起動・停止できるかを返します。</summary>
    bool CanControl { get; }

    Task<McpServerStatus> CheckAsync(CancellationToken cancellationToken);

    Task<McpServerStatus> StartAsync(CancellationToken cancellationToken);

    Task<McpServerStatus> StopAsync(CancellationToken cancellationToken);
}

/// <summary>Health URLの決定とHealth応答の分類を行います。</summary>
internal static class McpServerHealth
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 45440;
    public static readonly Uri DefaultHealthUri = new($"http://{DefaultHost}:{DefaultPort}/health");
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 設定ファイルの <c>server.mcpHost</c> と <c>server.mcpPort</c> からHealth URLを求めます。
    /// 設定が読めない、値が不正、またはloopbackでない場合は既定値を返します。
    /// </summary>
    public static Uri ResolveHealthUri(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson)) return DefaultHealthUri;
        try
        {
            using var document = JsonDocument.Parse(configurationJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (!document.RootElement.TryGetProperty("server", out var server) || server.ValueKind != JsonValueKind.Object) return DefaultHealthUri;
            var host = server.TryGetProperty("mcpHost", out var hostElement) && hostElement.ValueKind == JsonValueKind.String
                ? hostElement.GetString()
                : DefaultHost;
            var port = server.TryGetProperty("mcpPort", out var portElement) && portElement.ValueKind == JsonValueKind.Number && portElement.TryGetInt32(out var value)
                ? value
                : DefaultPort;
            if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535 || !IsLoopback(host)) return DefaultHealthUri;
            return new UriBuilder(Uri.UriSchemeHttp, host, port, "/health").Uri;
        }
        catch (JsonException)
        {
            return DefaultHealthUri;
        }
    }

    /// <summary>設定ファイルを読み、Health URLを求めます。読めない場合は既定値です。</summary>
    public static Uri ResolveHealthUriFromFile(string configurationPath)
    {
        try
        {
            return ResolveHealthUri(File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DefaultHealthUri;
        }
    }

    /// <summary>2xxかつ <c>status</c> が <c>ok</c> なら稼働中、それ以外の応答はエラーとします。</summary>
    public static McpServerStatus Classify(HttpStatusCode statusCode, string? body)
    {
        var code = (int)statusCode;
        if (code is < 200 or > 299) return new McpServerStatus(McpServerState.Error);
        try
        {
            using var document = JsonDocument.Parse(body ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out var status)
                || status.ValueKind != JsonValueKind.String
                || !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
            {
                return new McpServerStatus(McpServerState.Error);
            }

            var version = root.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String
                ? versionElement.GetString()
                : null;
            return new McpServerStatus(McpServerState.Running, string.IsNullOrWhiteSpace(version) ? null : version);
        }
        catch (JsonException)
        {
            return new McpServerStatus(McpServerState.Error);
        }
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
}

/// <summary>Monitorが起動するServerを、Health応答とインストール先のプロセスで管理します。</summary>
internal sealed class McpServerController(
    UserServerProcess? host,
    string serverExecutablePath,
    Uri healthUri,
    HttpClient http) : IMcpServerController
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    public bool CanControl => host is not null;

    public async Task<McpServerStatus> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(McpServerHealth.RequestTimeout);
        try
        {
            using var response = await http.GetAsync(healthUri, timeout.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return McpServerHealth.Classify(response.StatusCode, body);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or OperationCanceledException)
        {
            return new McpServerStatus(HasServerProcess() ? McpServerState.NotResponding : McpServerState.Stopped);
        }
    }

    public async Task<McpServerStatus> StartAsync(CancellationToken cancellationToken)
    {
        var current = await CheckAsync(cancellationToken).ConfigureAwait(false);
        if (current.State == McpServerState.Running) return current;
        if (host is null) throw new InvalidOperationException("Monitor does not manage Hataori Server in connect-only mode.");
        await Task.Run(host.Start, cancellationToken).ConfigureAwait(false);
        var deadline = DateTime.UtcNow + StartTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            var status = await CheckAsync(cancellationToken).ConfigureAwait(false);
            if (status.State == McpServerState.Running) return status;
        }

        throw new TimeoutException($"Hataori Server did not become healthy within {StartTimeout.TotalSeconds:0} seconds.");
    }

    public async Task<McpServerStatus> StopAsync(CancellationToken cancellationToken)
    {
        if (host is null) throw new InvalidOperationException("Monitor does not manage Hataori Server in connect-only mode.");
        // Monitorが起動したServerには標準入力を閉じて正常終了を求める。
        host.RequestStop();
        var deadline = DateTime.UtcNow + StopTimeout;
        while (DateTime.UtcNow < deadline && HasServerProcess())
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        foreach (var process in FindServerProcesses())
        {
            using (process)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
        }

        await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        if (HasServerProcess()) throw new TimeoutException("Hataori Server could not be stopped.");
        return await CheckAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool HasServerProcess()
    {
        var processes = FindServerProcesses();
        foreach (var process in processes) process.Dispose();
        return processes.Count > 0;
    }

    /// <summary>インストール先のServer実行ファイルとフルパスが一致するプロセスだけを返します。</summary>
    private List<Process> FindServerProcesses()
    {
        var expected = Path.GetFullPath(serverExecutablePath);
        var matches = new List<Process>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(expected)))
        {
            string? path = null;
            try { path = process.MainModule?.FileName; }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { }

            if (path is not null && string.Equals(Path.GetFullPath(path), expected, StringComparison.OrdinalIgnoreCase) && !process.HasExited)
            {
                matches.Add(process);
            }
            else
            {
                process.Dispose();
            }
        }

        return matches;
    }
}
