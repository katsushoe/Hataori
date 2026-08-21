using Hataori.Application.Control;
using Microsoft.Extensions.Logging;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

/// <summary>Hataoriの状態を読み取り専用で表示します。</summary>
public partial class MonitorForm : Form
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly string _pipeName;
    private readonly MonitorControlClient _client;
    private readonly MonitorErrorHandler _errors;
    private bool _refreshing;

    public MonitorForm(string pipeName, MonitorControlClient client, MonitorErrorHandler errors)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(errors);
        _pipeName = pipeName;
        _client = client;
        _errors = errors;
        InitializeComponent();
        ApplyLocalizedText();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RefreshSnapshotAsync().ConfigureAwait(true);
        refreshTimer.Start();
    }

    private async void RefreshTimerTick(object? sender, EventArgs e) => await RefreshSnapshotAsync().ConfigureAwait(true);

    private async void RefreshButtonClick(object? sender, EventArgs e) => await RefreshSnapshotAsync().ConfigureAwait(true);

    private async Task RefreshSnapshotAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var snapshot = await _client.GetSnapshotAsync(_pipeName, RequestTimeout, CancellationToken.None).ConfigureAwait(true);
            taskGrid.DataSource = snapshot.Tasks.Select(task => new { task.TaskName, task.AgentId, task.ProgressPercent, task.CurrentWork, task.LastActivityAtUtc, task.ConversationId, task.Status }).ToArray();
            agentGrid.DataSource = snapshot.Agents.ToArray();
            sessionGrid.DataSource = snapshot.Sessions.Select(session => new { session.ConversationId, session.AgentId, session.NativeSessionId, session.Status, session.LastUsedAtUtc }).ToArray();
            queueValueLabel.Text = snapshot.QueueCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
            serverValueLabel.Text = snapshot.System.Server;
            itogurumaValueLabel.Text = snapshot.System.Itoguruma;
            mcpValueLabel.Text = snapshot.System.Mcp;
            sqliteValueLabel.Text = snapshot.System.Sqlite;
            connectionStatusLabel.Text = DisplayLanguage.Text($"接続中: {_pipeName} / {DateTimeOffset.Now:T}", $"Connected: {_pipeName} / {DateTimeOffset.Now:T}");
        }
        catch (Exception exception)
        {
            var transient = exception is IOException or TimeoutException;
            connectionStatusLabel.Text = transient
                ? DisplayLanguage.Text($"接続エラー: {exception.Message}", $"Connection error: {exception.Message}")
                : DisplayLanguage.Text("表示データの取得に失敗しました。詳細はログを確認してください。", "Failed to retrieve display data. Check the log for details.");
            _errors.Report(exception, this, transient
                ? DisplayLanguage.Text("Serverへ接続できませんでした。Serverが起動しているか確認してください。", "Could not connect to Server. Verify that Server is running.")
                : DisplayLanguage.Text("表示データを読み込めませんでした。Monitorを再起動し、解消しない場合はログを管理者へ共有してください。", "Could not load display data. Restart Monitor and share the log with an administrator if the problem continues."), showDialog: !transient);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplyLocalizedText()
    {
        refreshButton.Text = DisplayLanguage.Text("更新", "Refresh");
        connectionStatusLabel.Text = DisplayLanguage.Text("未接続", "Not connected");
        tasksPage.Text = DisplayLanguage.Text("タスク", "Tasks");
        agentsPage.Text = DisplayLanguage.Text("エージェント", "Agents");
        sessionsPage.Text = DisplayLanguage.Text("会話／セッション", "Conversations / Sessions");
        statusPage.Text = DisplayLanguage.Text("状態", "Status");
        queueNameLabel.Text = DisplayLanguage.Text("キュー件数", "Queue count");
    }
}
