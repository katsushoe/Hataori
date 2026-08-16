using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;

namespace Hataori.Server;

/// <summary>Server異常終了後にRun、Session、Messageの状態を整合させます。</summary>
public sealed class StartupRecoveryService(
    AgentRunService runs,
    ConversationSessionService sessions,
    IMessageQueueRepository messages,
    IAgentProcessProbe processProbe,
    TimeProvider timeProvider)
{
    private const string RecoveryError = "Agent process was not running when Hataori restarted.";

    public async Task<StartupRecoveryResult> RecoverAsync(CancellationToken cancellationToken)
    {
        var activeRuns = (await runs.ListAsync(null, null, cancellationToken).ConfigureAwait(false))
            .Where(run => run.Status is AgentRunStatus.Starting or AgentRunStatus.Running)
            .ToArray();
        var survivors = new HashSet<(string ConversationId, string AgentId)>();
        var failedRuns = 0;
        var failedMessages = 0;
        var invalidatedSessions = 0;

        foreach (var run in activeRuns)
        {
            if (run.Status == AgentRunStatus.Running && run.ProcessId is int processId && processProbe.IsRunning(processId, run.StartedAtUtc))
            {
                survivors.Add((run.ConversationId, run.AgentId));
                continue;
            }

            await runs.FailAsync(run.RunId, null, RecoveryError, cancellationToken).ConfigureAwait(false);
            failedRuns++;
            var messageStatus = await messages.GetProcessingStatusAsync(run.MessageId, cancellationToken).ConfigureAwait(false);
            if (messageStatus is MessageProcessingStatus.Starting or MessageProcessingStatus.Running)
            {
                await messages.MarkFailedAsync(run.MessageId, RecoveryError, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                failedMessages++;
            }
        }

        var runningSessions = await sessions.ListAsync(ConversationSessionStatus.Running, null, cancellationToken).ConfigureAwait(false);
        foreach (var session in runningSessions)
        {
            if (survivors.Contains((session.ConversationId, session.AgentId)))
            {
                continue;
            }

            await sessions.InvalidateAsync(session.ConversationId, session.AgentId, cancellationToken).ConfigureAwait(false);
            invalidatedSessions++;
        }

        return new StartupRecoveryResult(failedRuns, failedMessages, invalidatedSessions, survivors.Count);
    }
}

/// <summary>起動復旧で変更・維持した件数です。</summary>
public sealed record StartupRecoveryResult(int FailedRuns, int FailedMessages, int InvalidatedSessions, int SurvivingRuns);
