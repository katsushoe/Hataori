using Hataori.Application.Control;

namespace Hataori.Monitor;

/// <summary>Hataoriの状態を読み取り専用で表示します。</summary>
public partial class MonitorForm : Form
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly string _pipeName;
    private readonly MonitorControlClient _client = new();
    private bool _refreshing;

    public MonitorForm(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
        InitializeComponent();
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
            connectionStatusLabel.Text = $"接続中: {_pipeName} / {DateTimeOffset.Now:T}";
        }
        catch (Exception exception) when (exception is IOException or TimeoutException)
        {
            connectionStatusLabel.Text = $"接続エラー: {exception.Message}";
        }
        finally
        {
            _refreshing = false;
        }
    }
}
