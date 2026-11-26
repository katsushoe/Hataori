using Hataori.Application.Localization;

namespace Hataori.Monitor;

/// <summary>タスクトレイの「MCPサーバー」メニュー（状態ランプ、状態確認、起動、停止）と「Serverを再起動」を扱います。</summary>
public partial class MonitorForm
{
    private readonly McpStatusLed _mcpLed = new();
    private IMcpServerController? _mcp;
    private McpServerStatus _mcpStatus = McpServerStatus.Unknown;
    private string? _mcpProgress;
    private bool _mcpChecking;

    internal ToolStripMenuItem McpServerMenuItem => mcpServerMenuItem;
    internal ToolStripMenuItem McpStatusLabel => mcpStatusLabel;
    internal ToolStripMenuItem McpCheckMenuItem => mcpCheckMenuItem;
    internal ToolStripMenuItem McpStartMenuItem => mcpStartMenuItem;
    internal ToolStripMenuItem McpStopMenuItem => mcpStopMenuItem;
    internal ToolStripMenuItem RestartServerMenuItem => restartServerMenuItem;
    internal McpServerStatus McpStatus => _mcpStatus;
    internal McpStatusLed McpLed => _mcpLed;

    internal void ConfigureMcpServer(IMcpServerController controller)
    {
        _mcp = controller;
        UpdateMcpMenu();
    }

    private void ApplyMcpMenuText()
    {
        mcpServerMenuItem.Text = DisplayLanguage.Text("MCPサーバー", "MCP server");
        mcpCheckMenuItem.Text = DisplayLanguage.Text("状態を確認", "Check status");
        mcpStartMenuItem.Text = DisplayLanguage.Text("起動", "Start");
        mcpStopMenuItem.Text = DisplayLanguage.Text("停止", "Stop");
        UpdateMcpMenu();
    }

    /// <summary>最新の状態に合わせて、ランプ・状態行・各操作の可否を更新します。</summary>
    internal void UpdateMcpMenu()
    {
        var image = _mcpLed.ImageFor(_mcpStatus.State);
        mcpServerMenuItem.Image = image;
        mcpStatusLabel.Image = image;
        mcpStatusLabel.Text = _mcpProgress ?? DescribeStatus(_mcpStatus);
        var canControl = _mcp?.CanControl == true;
        var idle = _mcp is not null && !_serverBusy && !_stopping;
        var running = _mcpStatus.State == McpServerState.Running;
        mcpCheckMenuItem.Enabled = idle;
        mcpStartMenuItem.Enabled = idle && canControl && !running;
        mcpStopMenuItem.Enabled = idle && canControl && _mcpStatus.State != McpServerState.Stopped;
        restartServerMenuItem.Enabled = !_serverBusy && !_stopping && _server?.IsRunning == true;
        exitMenuItem.Enabled = !_serverBusy;
    }

    internal static string DescribeStatus(McpServerStatus status) => status.State switch
    {
        McpServerState.Running => status.Version is { } version
            ? DisplayLanguage.Text($"MCPサーバー: 稼働中（{version}）", $"MCP server: running ({version})")
            : DisplayLanguage.Text("MCPサーバー: 稼働中", "MCP server: running"),
        McpServerState.NotResponding => DisplayLanguage.Text("MCPサーバー: 応答なし", "MCP server: not responding"),
        McpServerState.Error => DisplayLanguage.Text("MCPサーバー: エラー", "MCP server: error"),
        McpServerState.Stopped => DisplayLanguage.Text("MCPサーバー: 停止中", "MCP server: stopped"),
        _ => DisplayLanguage.Text("状態: 未確認", "Status: unknown"),
    };

    private async void TrayMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) => await RefreshMcpStatusAsync().ConfigureAwait(true);

    private async void McpServerMenuDropDownOpening(object? sender, EventArgs e) => await RefreshMcpStatusAsync().ConfigureAwait(true);

    private async void McpCheckMenuItemClick(object? sender, EventArgs e) => await CheckMcpServerAsync().ConfigureAwait(true);

    private async void McpStartMenuItemClick(object? sender, EventArgs e) => await StartMcpServerAsync().ConfigureAwait(true);

    private async void McpStopMenuItemClick(object? sender, EventArgs e) => await ConfirmAndStopMcpServerAsync().ConfigureAwait(true);

    private async void RestartServerMenuItemClick(object? sender, EventArgs e) => await ConfirmAndRestartServerAsync().ConfigureAwait(true);

    /// <summary>通知を出さずに状態を確認し直します。起動・停止の実行中は確認しません。</summary>
    internal async Task RefreshMcpStatusAsync()
    {
        if (_mcp is null || _serverBusy || _mcpChecking) return;
        _mcpChecking = true;
        try
        {
            var status = await _mcp.CheckAsync(_lifetime.Token).ConfigureAwait(true);
            if (IsDisposed) return;
            _mcpStatus = status;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (Exception exception)
        {
            _mcpStatus = new McpServerStatus(McpServerState.Error);
            _errors.Report(exception, this, DisplayLanguage.Text("MCPサーバーの状態を確認できませんでした。", "Could not check the MCP server status."), showDialog: false);
        }
        finally
        {
            _mcpChecking = false;
        }

        if (!IsDisposed) UpdateMcpMenu();
    }

    /// <summary>状態を確認し、結果をバルーン通知で表示します。</summary>
    internal async Task CheckMcpServerAsync()
    {
        await RefreshMcpStatusAsync().ConfigureAwait(true);
        if (IsDisposed || _mcp is null) return;
        Notify(DescribeStatus(_mcpStatus), _mcpStatus.State == McpServerState.Running ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    /// <summary>MCPサーバーを起動し、Healthが正常になるまで待ちます。</summary>
    internal Task StartMcpServerAsync()
    {
        var controller = _mcp;
        if (controller is null || !controller.CanControl || _mcpStatus.State == McpServerState.Running) return Task.CompletedTask;
        return RunMcpOperationAsync(
            DisplayLanguage.Text("状態: 起動中...", "Status: starting..."),
            () => controller.StartAsync(_lifetime.Token),
            DisplayLanguage.Text("MCPサーバーを起動しました。", "The MCP server started."),
            DisplayLanguage.Text("MCPサーバーを起動できませんでした。ログを確認してください。", "The MCP server could not be started. Check the log."));
    }

    /// <summary>確認後にMCPサーバーを停止します。</summary>
    internal Task ConfirmAndStopMcpServerAsync()
    {
        var controller = _mcp;
        if (controller is null || !controller.CanControl || _mcpStatus.State == McpServerState.Stopped || _serverBusy || _stopping) return Task.CompletedTask;
        if (!_dialogs.ConfirmServerAction(
                this,
                DisplayLanguage.Text(
                    "MCPサーバーを停止しますか？再び起動するまで、Hataoriを使うCodexとClaude Codeのすべてのセッションが切断されます。",
                    "Stop the MCP server? All Codex and Claude Code sessions that use Hataori are disconnected until it is started again."),
                Text))
        {
            return Task.CompletedTask;
        }

        return RunMcpOperationAsync(
            DisplayLanguage.Text("状態: 停止中...", "Status: stopping..."),
            () => controller.StopAsync(_lifetime.Token),
            DisplayLanguage.Text("MCPサーバーを停止しました。", "The MCP server stopped."),
            DisplayLanguage.Text("MCPサーバーを停止できませんでした。ログを確認してください。", "The MCP server could not be stopped. Check the log."));
    }

    /// <summary>確認後にServerを停止して起動し直します。</summary>
    internal Task ConfirmAndRestartServerAsync()
    {
        var server = _server;
        if (server is null || !server.IsRunning || _serverBusy || _stopping) return Task.CompletedTask;
        if (!_dialogs.ConfirmServerAction(
                this,
                DisplayLanguage.Text(
                    "Serverを再起動しますか？再起動が終わるまで、MCPクライアントとMonitorはServerへ接続できません。",
                    "Restart Server? MCP clients and Monitor cannot connect to Server until the restart completes."),
                Text))
        {
            return Task.CompletedTask;
        }

        return RunMcpOperationAsync(
            DisplayLanguage.Text("状態: 再起動中...", "Status: restarting..."),
            async () =>
            {
                await server.RestartAsync().ConfigureAwait(false);
                return _mcp is null ? McpServerStatus.Unknown : await _mcp.CheckAsync(_lifetime.Token).ConfigureAwait(false);
            },
            DisplayLanguage.Text("Serverを再起動しました。", "Server restarted."),
            DisplayLanguage.Text(
                "Serverを再起動できませんでした。Monitorを終了して起動し直し、解消しない場合はログを管理者へ共有してください。",
                "Could not restart Server. Exit and reopen Monitor, and share the log with an administrator if the problem continues."));
    }

    /// <summary>起動・停止・再起動を1件ずつ実行します。実行中は他の操作とMonitorの終了を受け付けません。</summary>
    private async Task RunMcpOperationAsync(string progress, Func<Task<McpServerStatus>> operation, string success, string failure)
    {
        if (_serverBusy || _stopping) return;
        _serverBusy = true;
        _mcpProgress = progress;
        UpdateMcpMenu();
        try
        {
            var status = await Task.Run(operation, _lifetime.Token).ConfigureAwait(true);
            if (IsDisposed) return;
            _mcpStatus = status;
            Notify(success, ToolTipIcon.Info);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (IsDisposed) return;
            _mcpStatus = new McpServerStatus(McpServerState.Error);
            _errors.Report(exception, this, failure, showDialog: false);
            Notify(failure, ToolTipIcon.Error);
        }
        finally
        {
            _serverBusy = false;
            _mcpProgress = null;
            if (!IsDisposed) UpdateMcpMenu();
        }
    }

    private void Notify(string message, ToolTipIcon icon)
    {
        connectionStatusLabel.Text = message;
        trayIcon.ShowBalloonTip(5000, Text, message, icon);
    }
}

/// <summary>ランプを色付きのまま表示し、選択・クリックできないサブメニューの状態行です。</summary>
internal sealed class McpStatusMenuItem : ToolStripMenuItem
{
    public override bool CanSelect => false;

    protected override void OnClick(EventArgs e)
    {
        // 状態表示専用のため、クリックしても何もしない。
    }
}
