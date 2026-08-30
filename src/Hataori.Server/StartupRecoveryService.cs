using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Application.Tasks;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;

namespace Hataori.Server;

/// <summary>Server異常終了後にRun、Session、Messageの状態を整合させます。</summary>
public sealed class StartupRecoveryService(
    ITaskRepository taskRepository,
    AgentRunService runs,
    IAgentRunRepository runRepository,
    ConversationSessionService sessions,
    IConversationSessionRepository sessionRepository,
    IMessageQueueRepository messages,
    IAgentProcessProbe processProbe,
    TimeProvider timeProvider)
{
    private const string RecoveryError = "Agent process was not running when Hataori restarted.";
    private const string RecoveryReplyError = "Reply delivery was interrupted when Hataori restarted.";

    public async Task<StartupRecoveryResult> RecoverAsync(CancellationToken cancellationToken)
    {
        await taskRepository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await runRepository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await sessionRepository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await messages.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var allRuns = await runs.ListAsync(null, null, cancellationToken).ConfigureAwait(false);
        var activeRuns = allRuns
            .Where(run => run.Status is AgentRunStatus.Starting or AgentRunStatus.Running)
            .ToArray();
        var completedMessageIds = allRuns
            .Where(run => run.Status == AgentRunStatus.Completed && !string.IsNullOrWhiteSpace(run.FinalMessage))
            .Select(run => run.MessageId)
            .ToHashSet(StringComparer.Ordinal);
        var survivors = new HashSet<(string ConversationId, string AgentId)>();
        var survivingMessageIds = new HashSet<string>(StringComparer.Ordinal);
        var failedRuns = 0;
        var failedMessages = 0;
        var recoveredReplies = 0;
        var invalidatedSessions = 0;

        foreach (var run in activeRuns)
        {
            if (run.Status == AgentRunStatus.Running && run.ProcessId is int processId && processProbe.IsRunning(processId, run.StartedAtUtc))
            {
                survivors.Add((run.ConversationId, run.AgentId));
                survivingMessageIds.Add(run.MessageId);
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

        var activeMessageIds = await messages.GetActiveExecutionMessageIdsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var messageId in activeMessageIds.Where(messageId => !survivingMessageIds.Contains(messageId)))
        {
            if (completedMessageIds.Contains(messageId))
            {
                var now = timeProvider.GetUtcNow();
                await messages.ScheduleReplyRetryAsync(messageId, RecoveryReplyError, 0, now, now, cancellationToken).ConfigureAwait(false);
                recoveredReplies++;
                continue;
            }

            await messages.MarkFailedAsync(messageId, RecoveryError, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            failedMessages++;
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

        return new StartupRecoveryResult(failedRuns, failedMessages, recoveredReplies, invalidatedSessions, survivors.Count);
    }
}

/// <summary>起動復旧で変更・維持した件数です。</summary>
public sealed record StartupRecoveryResult(int FailedRuns, int FailedMessages, int RecoveredReplies, int InvalidatedSessions, int SurvivingRuns);
