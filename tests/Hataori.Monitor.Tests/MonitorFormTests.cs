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

    [Fact]
    public void Tray_menu_places_mcp_server_submenu_between_separators_before_exit()
    {
        RunOnSta(() =>
        {
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureHost(null, startInTray: false);
            form.ConfigureMcpServer(new FakeMcpController());
            var items = form.McpServerMenuItem.Owner!.Items.Cast<ToolStripItem>().ToList();
            var index = items.IndexOf(form.McpServerMenuItem);
            Assert.IsType<ToolStripSeparator>(items[index - 1]);
            Assert.IsType<ToolStripSeparator>(items[index + 1]);
            Assert.Equal(items.Count - 1, index + 2);
            var sub = form.McpServerMenuItem.DropDownItems.Cast<ToolStripItem>().ToList();
            Assert.Same(form.McpStatusLabel, sub[0]);
            Assert.IsType<ToolStripSeparator>(sub[1]);
            Assert.Equal(new ToolStripItem[] { form.McpCheckMenuItem, form.McpStartMenuItem, form.McpStopMenuItem }, sub.Skip(2));
            Assert.Equal(McpServerState.Unknown, form.McpStatus.State);
            Assert.Same(form.McpLed.ImageFor(McpServerState.Stopped), form.McpStatusLabel.Image);
            Assert.Same(form.McpStatusLabel.Image, form.McpServerMenuItem.Image);
            Assert.False(form.RestartServerMenuItem.Available);
        });
    }

    [Theory]
    [InlineData(McpServerState.Running, false, true)]
    [InlineData(McpServerState.NotResponding, true, true)]
    [InlineData(McpServerState.Error, true, true)]
    [InlineData(McpServerState.Stopped, true, false)]
    public void Checked_status_sets_lamp_and_enables_start_or_stop(McpServerState state, bool startEnabled, bool stopEnabled)
    {
        RunOnStaAsync(async () =>
        {
            var controller = new FakeMcpController { Status = new McpServerStatus(state, state == McpServerState.Running ? "9.8.7.6" : null) };
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureMcpServer(controller);

            await form.RefreshMcpStatusAsync();

            Assert.Equal(state, form.McpStatus.State);
            Assert.Same(form.McpLed.ImageFor(state), form.McpStatusLabel.Image);
            Assert.Same(form.McpLed.ImageFor(state), form.McpServerMenuItem.Image);
            Assert.True(form.McpCheckMenuItem.Enabled);
            Assert.Equal(startEnabled, form.McpStartMenuItem.Enabled);
            Assert.Equal(stopEnabled, form.McpStopMenuItem.Enabled);
            if (state == McpServerState.Running) Assert.Contains("9.8.7.6", form.McpStatusLabel.Text);
        });
    }

    [Fact]
    public void Start_and_stop_are_disabled_when_monitor_does_not_manage_the_server()
    {
        RunOnStaAsync(async () =>
        {
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureMcpServer(new FakeMcpController { CanControl = false, Status = new McpServerStatus(McpServerState.Stopped) });
            await form.RefreshMcpStatusAsync();
            Assert.True(form.McpCheckMenuItem.Enabled);
            Assert.False(form.McpStartMenuItem.Enabled);
            Assert.False(form.McpStopMenuItem.Enabled);
        });
    }

    [Fact]
    public void Start_shows_progress_blocks_duplicates_and_reports_running()
    {
        RunOnStaAsync(async () =>
        {
            var controller = new FakeMcpController { Status = new McpServerStatus(McpServerState.Stopped), Pending = new TaskCompletionSource() };
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureMcpServer(controller);
            await form.RefreshMcpStatusAsync();

            var start = form.StartMcpServerAsync();
            Assert.Contains("...", form.McpStatusLabel.Text);
            Assert.False(form.McpStartMenuItem.Enabled);
            Assert.False(form.McpStopMenuItem.Enabled);
            await form.StartMcpServerAsync();
            controller.Pending.SetResult();
            await Pump(start);

            Assert.Equal(1, controller.Starts);
            Assert.Equal(McpServerState.Running, form.McpStatus.State);
            Assert.False(form.McpStartMenuItem.Enabled);
            Assert.True(form.McpStopMenuItem.Enabled);
        });
    }

    [Fact]
    public void Failed_start_sets_error_lamp()
    {
        RunOnStaAsync(async () =>
        {
            var controller = new FakeMcpController { Status = new McpServerStatus(McpServerState.Stopped), StartFailure = new TimeoutException("not healthy") };
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureMcpServer(controller);
            await form.RefreshMcpStatusAsync();

            await Pump(form.StartMcpServerAsync());

            Assert.Equal(McpServerState.Error, form.McpStatus.State);
            Assert.Same(form.McpLed.ImageFor(McpServerState.Error), form.McpStatusLabel.Image);
        });
    }

    [Theory]
    [InlineData(true, 1, McpServerState.Stopped)]
    [InlineData(false, 0, McpServerState.Running)]
    public void Stop_requires_confirmation_with_session_warning(bool confirm, int expectedStops, McpServerState expected)
    {
        RunOnStaAsync(async () =>
        {
            var dialogs = new FakeMonitorDialogs { ConfirmResult = confirm };
            var controller = new FakeMcpController { Status = new McpServerStatus(McpServerState.Running, "1.0") };
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), dialogs);
            form.ConfigureMcpServer(controller);
            await form.RefreshMcpStatusAsync();

            await Pump(form.ConfirmAndStopMcpServerAsync());

            var message = Assert.Single(dialogs.ServerActionConfirmations);
            Assert.Contains("Codex", message);
            Assert.Contains("Claude Code", message);
            Assert.Equal(expectedStops, controller.Stops);
            Assert.Equal(expected, form.McpStatus.State);
        });
    }

    [Theory]
    [InlineData(McpServerState.Running, "running")]
    [InlineData(McpServerState.Stopped, "stopped")]
    public void Mcp_submenu_renders_for_visual_review(McpServerState state, string name)
    {
        RunOnStaAsync(async () =>
        {
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), new FakeMonitorDialogs());
            form.ConfigureMcpServer(new FakeMcpController { Status = new McpServerStatus(state, state == McpServerState.Running ? "3.2.5.0" : null) });
            await form.RefreshMcpStatusAsync();
            var dropDown = form.McpServerMenuItem.DropDown;
            dropDown.Opacity = 0;
            dropDown.Show(Point.Empty);
            try
            {
                Assert.True(dropDown.Width >= dropDown.PreferredSize.Width, $"dropdown {dropDown.Width} < preferred {dropDown.PreferredSize.Width}");
                Assert.True(form.McpStatusLabel.Width >= form.McpStatusLabel.GetPreferredSize(Size.Empty).Width, "status text is truncated");
                using var bitmap = new Bitmap(dropDown.Width, dropDown.Height);
                dropDown.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dropDown.Size));
                bitmap.Save(Path.Combine(ValidationDirectory(), $"monitor-mcp-menu-{name}.png"));
            }
            finally { dropDown.Close(); }
        });
    }

    [Fact]
    public void Confirmed_restart_stops_and_starts_server_once_and_blocks_concurrent_requests()
    {
        RunOnStaAsync(async () =>
        {
            var dialogs = new FakeMonitorDialogs { ConfirmResult = true };
            var host = new FakeServerHost { Pending = new TaskCompletionSource() };
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), dialogs);
            form.ConfigureHost(host, startInTray: false);

            var restart = form.ConfirmAndRestartServerAsync();
            Assert.False(form.RestartServerMenuItem.Enabled);
            await form.ConfirmAndRestartServerAsync();
            host.Pending.SetResult();
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!restart.IsCompleted && DateTime.UtcNow < deadline) System.Windows.Forms.Application.DoEvents();
            await restart;

            Assert.Equal(1, host.Restarts);
            Assert.Single(dialogs.ServerActionConfirmations);
            Assert.True(form.RestartServerMenuItem.Enabled);
        });
    }

    [Fact]
    public void Declined_restart_does_not_touch_server()
    {
        RunOnStaAsync(async () =>
        {
            var dialogs = new FakeMonitorDialogs { ConfirmResult = false };
            var host = new FakeServerHost();
            using var form = CreateForm(new FakeMonitorControlClient(Snapshot("active")), dialogs);
            form.ConfigureHost(host, startInTray: false);

            await form.ConfirmAndRestartServerAsync();

            Assert.Equal(0, host.Restarts);
            Assert.Single(dialogs.ServerActionConfirmations);
        });
    }

    private static async Task Pump(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            // STAスレッドではawaitすると継続が実行されないため、メッセージを処理しながら同期的に待つ。
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
        await task;
    }

    private static string ValidationDirectory()
    {
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/validation"));
        Directory.CreateDirectory(directory);
        return directory;
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

        public List<string> ServerActionConfirmations { get; } = [];

        public bool ConfirmServerAction(IWin32Window owner, string message, string title)
        {
            ServerActionConfirmations.Add(message);
            return ConfirmResult;
        }
    }

    private sealed class FakeMcpController : IMcpServerController
    {
        public bool CanControl { get; init; } = true;
        public McpServerStatus Status { get; set; } = McpServerStatus.Unknown;
        public TaskCompletionSource? Pending { get; init; }
        public Exception? StartFailure { get; init; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }

        public Task<McpServerStatus> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(Status);

        public async Task<McpServerStatus> StartAsync(CancellationToken cancellationToken)
        {
            Starts++;
            if (Pending is not null) await Pending.Task.ConfigureAwait(false);
            if (StartFailure is not null) throw StartFailure;
            return Status = new McpServerStatus(McpServerState.Running, "1.0");
        }

        public Task<McpServerStatus> StopAsync(CancellationToken cancellationToken)
        {
            Stops++;
            return Task.FromResult(Status = new McpServerStatus(McpServerState.Stopped));
        }
    }
    private sealed class FakeServerHost : IMonitorServerHost
    {
        public TaskCompletionSource? Pending { get; init; }
        public bool Running { get; set; } = true;
        public int Restarts { get; private set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public bool IsRunning => Running;

        public void Start()
        {
            Starts++;
            Running = true;
        }

        public Task StopAsync()
        {
            Stops++;
            Running = false;
            return Task.CompletedTask;
        }

        public Task RestartAsync()
        {
            Restarts++;
            return Pending?.Task ?? Task.CompletedTask;
        }
    }
}
