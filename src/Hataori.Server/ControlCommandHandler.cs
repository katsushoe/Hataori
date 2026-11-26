using Hataori.Application.Activation;
using Hataori.Application.Control;
using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Application.Tasks;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Core.Tasks;
using Microsoft.Extensions.Hosting;

namespace Hataori.Server;

/// <summary>ローカルControl Pipeから受け取った管理コマンドを処理します。</summary>
public sealed class ControlCommandHandler
{
    /// <summary>Monitorスナップショットへ含める終了済み項目の上限です。実行中・待機中の項目は上限に関係なく含めます。</summary>
    public const int MonitorRecentItemLimit = 200;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ITaskRepository _tasks;
    private readonly TaskService _taskService;
    private readonly IConversationSessionRepository _sessions;
    private readonly IAgentRunRepository _runs;
    private readonly IMessageQueueRepository _queue;
    private readonly ItogurumaConnectionState _itogurumaState;
    private readonly ActivationManager _activation;

    public ControlCommandHandler(IHostApplicationLifetime lifetime, TimeProvider timeProvider, ITaskRepository tasks, TaskService taskService, IConversationSessionRepository sessions, IAgentRunRepository runs, IMessageQueueRepository queue, ItogurumaConnectionState itogurumaState, ActivationManager activation)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(taskService);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(itogurumaState);
        ArgumentNullException.ThrowIfNull(activation);
        _lifetime = lifetime;
        _timeProvider = timeProvider;
        _tasks = tasks;
        _taskService = taskService;
        _sessions = sessions;
        _runs = runs;
        _queue = queue;
        _itogurumaState = itogurumaState;
        _activation = activation;
    }

    public async Task<ControlResponse> HandleAsync(ControlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.Equals(request.Command, "status", StringComparison.OrdinalIgnoreCase))
        {
            return new ControlResponse(true, "running", _timeProvider.GetUtcNow());
        }

        if (string.Equals(request.Command, "monitor", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateMonitorResponseAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(request.Command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            _lifetime.StopApplication();
            return new ControlResponse(true, "stopping", _timeProvider.GetUtcNow());
        }

        if (string.Equals(request.Command, "reload", StringComparison.OrdinalIgnoreCase))
        {
            return new ControlResponse(true, "reload_on_change_enabled", _timeProvider.GetUtcNow());
        }

        if (string.Equals(request.Command, "agent-cancel", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAgentCancelAsync(request.Argument, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(request.Command, "task-cancel", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleTaskCancelAsync(request.Argument, cancellationToken).ConfigureAwait(false);
        }

        return new ControlResponse(false, "unknown_command", _timeProvider.GetUtcNow());
    }

    private async Task<ControlResponse> HandleTaskCancelAsync(string? taskId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return new ControlResponse(false, "missing_argument", _timeProvider.GetUtcNow());
        }

        try
        {
            await _taskService.CancelAsync(taskId, "Cancelled from Monitor.", cancellationToken).ConfigureAwait(false);
            return new ControlResponse(true, "cancelled", _timeProvider.GetUtcNow());
        }
        catch (KeyNotFoundException)
        {
            return new ControlResponse(false, "not_found", _timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException)
        {
            return new ControlResponse(false, "invalid_state", _timeProvider.GetUtcNow());
        }
    }

    private async Task<ControlResponse> HandleAgentCancelAsync(string? runId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return new ControlResponse(false, "missing_argument", _timeProvider.GetUtcNow());
        }

        try
        {
            var hadLiveProcess = await _activation.RequestRunCancellationAsync(runId, cancellationToken).ConfigureAwait(false);
            return new ControlResponse(true, hadLiveProcess ? "cancelled" : "cancelled_db_only", _timeProvider.GetUtcNow());
        }
        catch (KeyNotFoundException)
        {
            return new ControlResponse(false, "not_found", _timeProvider.GetUtcNow());
        }
    }

    private async Task<ControlResponse> CreateMonitorResponseAsync(CancellationToken cancellationToken)
    {
        var tasks = await _tasks.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        var sessions = await _sessions.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        var runs = await _runs.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        var queued = await _queue.ListAsync(null, cancellationToken).ConfigureAwait(false);
        var agents = runs.GroupBy(run => run.AgentId, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            var active = group.Where(run => run.Status is AgentRunStatus.Starting or AgentRunStatus.Running).ToArray();
            var latest = group.OrderByDescending(run => run.StartedAtUtc ?? run.QueuedAtUtc).First();
            var state = active.Length > 0 ? "running" : group.Any(run => run.Status == AgentRunStatus.Queued) ? "queued" : "idle";
            return new MonitorAgentStatus(group.Key, active.FirstOrDefault()?.ConversationId ?? latest.ConversationId, state, active.Length);
        }).OrderBy(agent => agent.AgentId, StringComparer.OrdinalIgnoreCase).ToArray();
        // 3秒ごとの全件転送を避けるため、実行中の項目と新しい項目だけを返す（Agent状態は全Runから集計する）。
        var monitorTasks = SelectRecent(tasks, task => task.Status == HataoriTaskStatus.Active, task => task.LastActivityAtUtc).Select(task => new MonitorTask(
            task.WorkspaceId, task.TaskId, task.TaskName, task.AgentId, task.ConversationId, task.Status.ToString().ToLowerInvariant(),
            task.CurrentWork, task.ProgressPercent, task.LastActivityAtUtc)).ToArray();
        var monitorSessions = SelectRecent(sessions, session => session.Status == ConversationSessionStatus.Running, session => session.LastUsedAtUtc).Select(session => new MonitorSession(
            session.WorkspaceId, session.ConversationId, session.AgentId, session.NativeSessionId,
            session.Status.ToString().ToLowerInvariant(), session.LastUsedAtUtc)).ToArray();
        var monitorRuns = SelectRecent(runs, run => run.Status is AgentRunStatus.Queued or AgentRunStatus.Starting or AgentRunStatus.Running, run => run.StartedAtUtc ?? run.QueuedAtUtc).Select(run => new MonitorRun(
            run.WorkspaceId, run.RunId, run.MessageId, run.ConversationId, run.AgentId, run.Status.ToString().ToLowerInvariant(),
            run.QueuedAtUtc, run.StartedAtUtc, run.EndedAtUtc, run.Error)).ToArray();
        var snapshot = new MonitorSnapshot(monitorTasks, agents, monitorSessions, monitorRuns, queued.Count, new MonitorSystemStatus("running", _itogurumaState.Value, "running", "connected"));
        return new ControlResponse(true, "running", _timeProvider.GetUtcNow(), snapshot);
    }

    /// <summary>実行中の項目をすべて残し、それ以外は新しい順に上限件数まで選んで、全体を新しい順に返します。</summary>
    private static IEnumerable<T> SelectRecent<T>(IEnumerable<T> items, Func<T, bool> isLive, Func<T, DateTimeOffset> activity)
    {
        var ordered = items.OrderByDescending(activity).ToArray();
        var recent = ordered.Where(item => !isLive(item)).Take(MonitorRecentItemLimit).ToHashSet();
        return ordered.Where(item => isLive(item) || recent.Contains(item));
    }
}
