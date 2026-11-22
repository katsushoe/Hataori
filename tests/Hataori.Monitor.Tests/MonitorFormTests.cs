using System.Threading;
using Hataori.Application.Control;
using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class MonitorFormTests
{
    [Fact]
    public void RefreshSnapshot_updates_all_monitor_views_and_status()
    {
        RunOnStaAsync(async () =>
        {
            var client = new FakeMonitorControlClient(Snapshot("active", "running"));
            using var form = CreateForm(client, new FakeMonitorDialogs());

            await form.RefreshSnapshotAsync();

            Assert.Equal(1, form.TaskGrid.Rows.Count);
            Assert.Equal("task-1", form.TaskGrid.Rows[0].Cells["TaskId"].Value);
            Assert.Equal(1, form.AgentGrid.Rows.Count);
            Assert.Equal(1, form.SessionGrid.Rows.Count);
            Assert.Equal(1, form.RunGrid.Rows.Count);
            Assert.Equal("3", form.QueueValue);
            Assert.Equal("healthy", form.ServerValue);
            Assert.Contains("test-pipe", form.ConnectionStatus);

            form.CreateControl();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            Assert.Equal(form.Width, bitmap.Width);
            Assert.Equal(form.Height, bitmap.Height);
        });
    }

    [Fact]
    public void Selection_enables_cancellation_only_for_cancellable_states()
    {
        RunOnStaAsync(async () =>
        {
            var client = new FakeMonitorControlClient(Snapshot("active", "queued", "starting", "running", "completed"));
            using var form = CreateForm(client, new FakeMonitorDialogs());
            await form.RefreshSnapshotAsync();

            SelectRow(form.TaskGrid, 0);
            form.UpdateActionButtons();
            Assert.True(form.CancelTaskButton.Enabled);
            form.TaskGrid.DataSource = new[] { new { TaskId = "done", Status = "completed" } };
            SelectRow(form.TaskGrid, 0);
            form.UpdateActionButtons();
            Assert.False(form.CancelTaskButton.Enabled);

            foreach (var status in new[] { "queued", "starting", "running" })
            {
                form.RunGrid.DataSource = new[] { new { RunId = "run", Status = status } };
                SelectRow(form.RunGrid, 0);
                form.UpdateActionButtons();
                Assert.True(form.CancelRunButton.Enabled);
            }

            form.RunGrid.DataSource = new[] { new { RunId = "run", Status = "completed" } };
            SelectRow(form.RunGrid, 0);
            form.UpdateActionButtons();
            Assert.False(form.CancelRunButton.Enabled);
        });
    }

    [Fact]
    public void Cancel_task_button_confirms_sends_id_and_refreshes_the_view()
    {
        RunOnStaAsync(async () =>
        {
            var client = new FakeMonitorControlClient(Snapshot("active", "running"), Snapshot("cancelled", "running"));
            var dialogs = new FakeMonitorDialogs { ConfirmResult = true };
            using var form = CreateForm(client, dialogs);
            await form.RefreshSnapshotAsync();
            SelectRow(form.TaskGrid, 0);

            await form.ConfirmAndCancelTaskAsync();

            Assert.Equal(new[] { "task-1" }, client.CancelledTasks);
            Assert.Contains("task-1", dialogs.Confirmations.Single());
            Assert.Equal("cancelled", form.TaskGrid.Rows[0].Cells["Status"].Value);
        });
    }

    [Fact]
    public void Cancel_run_button_confirms_cancels_and_reports_rejected_response()
    {
        RunOnStaAsync(async () =>
        {
            var client = new FakeMonitorControlClient(Snapshot("active", "running"), Snapshot("active", "running"))
            {
                RunResponse = new ControlResponse(false, "already_finished", DateTimeOffset.UtcNow),
            };
            var dialogs = new FakeMonitorDialogs { ConfirmResult = true };
            using var form = CreateForm(client, dialogs);
            await form.RefreshSnapshotAsync();
            SelectRow(form.RunGrid, 0);

            await form.ConfirmAndCancelRunAsync();

            Assert.Equal(new[] { "run-1" }, client.CancelledRuns);
            Assert.Single(dialogs.Failures);
            Assert.Contains("already_finished", dialogs.Failures[0]);
        });
    }

    [Fact]
    public void Declined_confirmation_does_not_send_cancel_request()
    {
        RunOnStaAsync(async () =>
        {
            var client = new FakeMonitorControlClient(Snapshot("active", "running"));
            var dialogs = new FakeMonitorDialogs { ConfirmResult = false };
            using var form = CreateForm(client, dialogs);
            await form.RefreshSnapshotAsync();
            SelectRow(form.TaskGrid, 0);

            await form.ConfirmAndCancelTaskAsync();

            Assert.Empty(client.CancelledTasks);
            Assert.Empty(client.CancelledRuns);
            Assert.Single(dialogs.Confirmations);
        });
    }

    private static MonitorForm CreateForm(FakeMonitorControlClient client, FakeMonitorDialogs dialogs) =>
        new("test-pipe", client, new MonitorErrorHandler(new MonitorFileLogger()), dialogs);

    private static void SelectRow(DataGridView grid, int index) => grid.CurrentCell = grid.Rows[index].Cells[0];

    private static MonitorSnapshot Snapshot(string taskStatus, params string[] runStatuses) => new(
        [new MonitorTask("workspace-1", "task-1", "Test task", "agent-1", "conversation-1", taskStatus, "Working", 42, DateTimeOffset.UtcNow)],
        [new MonitorAgentStatus("agent-1", "conversation-1", "active", 1)],
        [new MonitorSession("workspace-1", "conversation-1", "agent-1", "native-1", "active", DateTimeOffset.UtcNow)],
        runStatuses.Select((status, index) => new MonitorRun("workspace-1", $"run-{index + 1}", "message-1", "conversation-1", "agent-1", status, DateTimeOffset.UtcNow, null, null, null)).ToArray(),
        3,
        new MonitorSystemStatus("healthy", "connected", "available", "healthy"));

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
            finally { completed.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(15)), "WinForms STA test did not complete within 15 seconds.");
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException($"WinForms STA test failed: {failure}");
    }

    private static void RunOnStaAsync(Func<Task> action)
    {
        Exception? failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(async () =>
        {
            try { await action().ConfigureAwait(false); }
            catch (Exception exception) { failure = exception; }
            finally { completed.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(15)), "WinForms STA test did not complete within 15 seconds.");
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException($"WinForms STA test failed: {failure}");
    }

    private sealed class FakeMonitorControlClient(params MonitorSnapshot[] snapshots) : IMonitorControlClient
    {
        private readonly Queue<MonitorSnapshot> _snapshots = new(snapshots);
        public List<string> CancelledTasks { get; } = [];
        public List<string> CancelledRuns { get; } = [];
        public ControlResponse RunResponse { get; set; } = new(true, "cancelled", DateTimeOffset.UtcNow);

        public Task<MonitorSnapshot> GetSnapshotAsync(string pipeName, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(_snapshots.Count > 1 ? _snapshots.Dequeue() : _snapshots.Peek());

        public Task<ControlResponse> CancelTaskAsync(string pipeName, string taskId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            CancelledTasks.Add(taskId);
            return Task.FromResult(new ControlResponse(true, "cancelled", DateTimeOffset.UtcNow));
        }

        public Task<ControlResponse> CancelRunAsync(string pipeName, string runId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            CancelledRuns.Add(runId);
            return Task.FromResult(RunResponse);
        }
    }

    private sealed class FakeMonitorDialogs : IMonitorDialogs
    {
        public bool ConfirmResult { get; init; }
        public List<string> Confirmations { get; } = [];
        public List<string> Failures { get; } = [];

        public bool ConfirmCancel(IWin32Window owner, string message, string title)
        {
            Confirmations.Add(message);
            return ConfirmResult;
        }

        public void ShowCancellationFailure(IWin32Window owner, string message, string title) => Failures.Add(message);
    }
}
