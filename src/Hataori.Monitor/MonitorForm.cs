using Hataori.Application.Control;
using Microsoft.Extensions.Logging;
using Hataori.Application.Localization;

namespace Hataori.Monitor;

/// <summary>Hataoriの状態表示と安全なキャンセル操作を提供します。</summary>
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
            taskGrid.DataSource = snapshot.Tasks.Select(task => new { task.TaskId, task.WorkspaceId, task.TaskName, task.AgentId, task.ProgressPercent, task.CurrentWork, task.LastActivityAtUtc, task.ConversationId, task.Status }).ToArray();
            agentGrid.DataSource = snapshot.Agents.ToArray();
            sessionGrid.DataSource = snapshot.Sessions.Select(session => new { session.ConversationId, session.AgentId, session.NativeSessionId, session.Status, session.LastUsedAtUtc }).ToArray();
            runGrid.DataSource = snapshot.Runs.Select(run => new { run.RunId, run.WorkspaceId, run.AgentId, run.ConversationId, run.Status, run.QueuedAtUtc, run.StartedAtUtc, run.EndedAtUtc, run.Error }).ToArray();
            queueValueLabel.Text = snapshot.QueueCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
            serverValueLabel.Text = snapshot.System.Server;
            itogurumaValueLabel.Text = snapshot.System.Itoguruma;
            mcpValueLabel.Text = snapshot.System.Mcp;
            sqliteValueLabel.Text = snapshot.System.Sqlite;
            connectionStatusLabel.Text = DisplayLanguage.Text($"接続中: {_pipeName} / {DateTimeOffset.Now:T}", $"Connected: {_pipeName} / {DateTimeOffset.Now:T}");
            UpdateActionButtons();
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

    private void GridSelectionChanged(object? sender, EventArgs e) => UpdateActionButtons();

    private void UpdateActionButtons()
    {
        cancelTaskButton.Enabled = !_refreshing && TryGetSelectedValue(taskGrid, "Status", out var taskStatus)
            && string.Equals(taskStatus, "active", StringComparison.OrdinalIgnoreCase);
        cancelRunButton.Enabled = !_refreshing && TryGetSelectedValue(runGrid, "Status", out var runStatus)
            && IsCancellableRunStatus(runStatus);
    }

    private async void CancelTaskButtonClick(object? sender, EventArgs e)
    {
        if (!TryGetSelectedValue(taskGrid, "TaskId", out var taskId) || !ConfirmCancel("Task", taskId))
        {
            return;
        }

        await ExecuteCancelAsync(() => _client.CancelTaskAsync(_pipeName, taskId, RequestTimeout, CancellationToken.None)).ConfigureAwait(true);
    }

    private async void CancelRunButtonClick(object? sender, EventArgs e)
    {
        if (!TryGetSelectedValue(runGrid, "RunId", out var runId) || !ConfirmCancel("Agent Run", runId))
        {
            return;
        }

        await ExecuteCancelAsync(() => _client.CancelRunAsync(_pipeName, runId, RequestTimeout, CancellationToken.None)).ConfigureAwait(true);
    }

    private async Task ExecuteCancelAsync(Func<Task<ControlResponse>> operation)
    {
        cancelTaskButton.Enabled = false;
        cancelRunButton.Enabled = false;
        try
        {
            var response = await operation().ConfigureAwait(true);
            if (!response.Success)
            {
                MessageBox.Show(this, DisplayLanguage.Text($"キャンセルできませんでした: {response.Status}", $"Cancellation failed: {response.Status}"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await RefreshSnapshotAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _errors.Report(exception, this, DisplayLanguage.Text("キャンセル操作に失敗しました。詳細はログを確認してください。", "Cancellation failed. Check the log for details."), showDialog: true);
        }
        finally
        {
            UpdateActionButtons();
        }
    }

    private bool ConfirmCancel(string targetType, string targetId) => MessageBox.Show(
        this,
        DisplayLanguage.Text($"{targetType} '{targetId}' をキャンセルしますか？", $"Cancel {targetType} '{targetId}'?"),
        Text,
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Warning,
        MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private static bool TryGetSelectedValue(DataGridView grid, string propertyName, out string value)
    {
        value = string.Empty;
        if (grid.CurrentRow?.DataBoundItem is null)
        {
            return false;
        }

        value = grid.CurrentRow.DataBoundItem.GetType().GetProperty(propertyName)?.GetValue(grid.CurrentRow.DataBoundItem)?.ToString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool IsCancellableRunStatus(string status) =>
        string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "starting", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "running", StringComparison.OrdinalIgnoreCase);

    private void ApplyLocalizedText()
    {
        refreshButton.Text = DisplayLanguage.Text("更新", "Refresh");
        cancelTaskButton.Text = DisplayLanguage.Text("Taskをキャンセル", "Cancel Task");
        cancelRunButton.Text = DisplayLanguage.Text("Agent Runを停止", "Cancel Agent Run");
        connectionStatusLabel.Text = DisplayLanguage.Text("未接続", "Not connected");
        tasksPage.Text = DisplayLanguage.Text("タスク", "Tasks");
        agentsPage.Text = DisplayLanguage.Text("エージェント", "Agents");
        sessionsPage.Text = DisplayLanguage.Text("会話／セッション", "Conversations / Sessions");
        runsPage.Text = DisplayLanguage.Text("Agent実行", "Agent Runs");
        statusPage.Text = DisplayLanguage.Text("状態", "Status");
        queueNameLabel.Text = DisplayLanguage.Text("キュー件数", "Queue count");
    }
}
