using System.Globalization;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

public partial class MonitorForm
{
    private readonly CancellationTokenSource _lifetime = new();
    private MonitorSettings _settings = new();
    private MonitorSettingsStore? _settingsStore;
    private MonitorUpdateClient? _updater;
    private bool _exitRequested;
    private bool _checkingUpdate;
    private bool _skipUpdate;
    private IMonitorServerHost? _server;
    private bool _startInTray;
    private bool _stopping;
    private bool _serverBusy;

    internal void ConfigureHost(IMonitorServerHost? server, bool startInTray)
    {
        _server = server;
        _startInTray = startInTray;
        // Monitorが起動したServerだけを操作できる。接続専用起動では他者のServerに触れない。
        restartServerMenuItem.Visible = server is not null;
        UpdateMcpMenu();
    }

    internal void ConfigureRuntime(MonitorSettings settings, MonitorSettingsStore store, MonitorUpdateClient updater, bool skipUpdate)
    {
        _settingsStore = store;
        _updater = updater;
        _skipUpdate = skipUpdate;
        ApplySettings(settings);
    }

    internal void ApplySettings(MonitorSettings settings)
    {
        settings.Validate();
        _settings = settings;
        refreshTimer.Interval = settings.RefreshSeconds * 1000;
        var culture = CultureInfo.GetCultureInfo(settings.Language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        ApplyLocalizedText();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        if (ShouldHideOnClose(e.CloseReason, _settings.CloseToTray, _exitRequested))
        {
            e.Cancel = true;
            HideToTray();
        }
        else if (_serverBusy && e.CloseReason == CloseReason.UserClosing)
        {
            // 起動・停止・再起動の完了前に終了すると、Serverが停止されずに残る可能性がある。
            e.Cancel = true;
        }
        else if (_server is not null && _server.IsRunning && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            if (_stopping) return;
            _stopping = true;
            try
            {
                // Let WinForms finish processing the current Close before issuing a second one.
                await Task.Yield();
                await _server.StopAsync();
                Close();
            }
            catch (Exception exception)
            {
                _errors.Report(exception, this, DisplayLanguage.Text("Serverを停止できませんでした。しばらく待ってから再試行してください。", "Server shutdown did not complete. Wait and try again."), showDialog: true);
            }
            finally { _stopping = false; }
        }
        else
        {
            refreshTimer.Stop();
            trayIcon.Visible = false;
            _lifetime.Cancel();
        }
    }

    internal static bool ShouldHideOnClose(CloseReason reason, bool closeToTray, bool exitRequested) =>
        reason == CloseReason.UserClosing && closeToTray && !exitRequested;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && trayIcon is not null) HideToTray();
    }

    internal void HideToTray()
    {
        trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
    }

    internal void RestoreFromTray()
    {
        if (IsDisposed) return;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OpenMenuItemClick(object? sender, EventArgs e) => RestoreFromTray();

    private void ExitMenuItemClick(object? sender, EventArgs e) => RequestExit();

    internal void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    private void SettingsButtonClick(object? sender, EventArgs e)
    {
        if (_settingsStore is null) return;
        RestoreFromTray();
        using var form = new MonitorSettingsForm(_settings, _settingsStore, _errors);
        if (form.ShowDialog(this) == DialogResult.OK) ApplySettings(form.Settings);
    }

    private async void UpdateButtonClick(object? sender, EventArgs e) => await CheckForUpdateAsync(manual: true).ConfigureAwait(true);

    internal async Task CheckForUpdateAsync(bool manual)
    {
        if (_updater is null || _checkingUpdate) return;
        _checkingUpdate = true;
        updateButton.Enabled = false;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            connectionStatusLabel.Text = DisplayLanguage.Text("アプリの更新を確認中…", "Checking for application updates…");
            var version = typeof(MonitorForm).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            var release = await _updater.CheckAsync(version, deadline.Token).ConfigureAwait(true);
            if (release is null)
            {
                if (manual) MessageBox.Show(this, DisplayLanguage.Text("最新バージョンです。", "You have the latest version."), Text);
                return;
            }
            var directory = Path.Combine(MonitorSettingsStore.UserDirectory, "updates", Guid.NewGuid().ToString("N"));
            var package = await _updater.DownloadAsync(release, directory, deadline.Token).ConfigureAwait(true);
            deadline.Token.ThrowIfCancellationRequested();
            MonitorUpdateInstaller.Start(package, release, _pipeName, _settings.Language, !Visible);
            RequestExit();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (IsDisposed) return;
            connectionStatusLabel.Text = DisplayLanguage.Text("アプリの更新に失敗しました。現在のバージョンを使用できます。", "Application update failed. You can use the current version.");
            _errors.Report(exception, this, connectionStatusLabel.Text, showDialog: manual);
            if (!manual)
            {
                trayIcon.ShowBalloonTip(6000, Text, connectionStatusLabel.Text, ToolTipIcon.Warning);
            }
        }
        finally
        {
            _checkingUpdate = false;
            if (!IsDisposed) updateButton.Enabled = true;
        }
    }

    private void ApplyLifecycleText()
    {
        settingsButton.Text = DisplayLanguage.Text("設定", "Settings");
        updateButton.Text = DisplayLanguage.Text("アプリの更新", "Update application");
        openMenuItem.Text = DisplayLanguage.Text("Monitorを開く", "Open Monitor");
        settingsMenuItem.Text = DisplayLanguage.Text("設定", "Settings");
        restartServerMenuItem.Text = DisplayLanguage.Text("Serverを再起動", "Restart Server");
        ApplyMcpMenuText();
        exitMenuItem.Text = DisplayLanguage.Text("終了", "Exit");
        Text = "Hataori Monitor " + typeof(MonitorForm).Assembly.GetName().Version;
    }
}
