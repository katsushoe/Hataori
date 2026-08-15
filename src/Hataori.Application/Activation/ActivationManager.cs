using Hataori.Application.Agents;
using Hataori.Application.Messages;
using Hataori.Application.Runs;
using Hataori.Application.Sessions;
using Hataori.Application.Itoguruma;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;

namespace Hataori.Application.Activation;

public sealed record ActivationRequest(string WorkingDirectory, string HataoriRoot, string McpUrl);
public sealed record ActivationResult(string MessageId, string? RunId, string? ReplyMessageId, bool Succeeded, string? Error);

/// <summary>
/// Queue、Session、Run、Agent Driverを1回のActivationとして調停します。
/// </summary>
public sealed class ActivationManager
{
    private readonly IMessageQueueRepository _messageQueue;
    private readonly IConversationMutex _conversationMutex;
    private readonly ConversationSessionService _sessions;
    private readonly AgentRunService _runs;
    private readonly IReadOnlyDictionary<string, IAgentDriver> _drivers;
    private readonly TimeProvider _timeProvider;
    private readonly IItogurumaClient _itoguruma;
    private readonly ReplyRetryManager _replyRetries;

    public ActivationManager(
        IMessageQueueRepository messageQueue,
        IConversationMutex conversationMutex,
        ConversationSessionService sessions,
        AgentRunService runs,
        IEnumerable<IAgentDriver> drivers,
        TimeProvider timeProvider,
        IItogurumaClient itoguruma,
        ReplyRetryManager replyRetries)
    {
        _messageQueue = messageQueue;
        _conversationMutex = conversationMutex;
        _sessions = sessions;
        _runs = runs;
        _drivers = drivers.ToDictionary(driver => driver.AgentType, StringComparer.OrdinalIgnoreCase);
        _timeProvider = timeProvider;
        _itoguruma = itoguruma;
        _replyRetries = replyRetries;
    }

    public async Task<ActivationResult?> ProcessNextAsync(ActivationRequest request, CancellationToken cancellationToken)
        => await ProcessNextAsync(request, null, cancellationToken).ConfigureAwait(false);

    public async Task<ActivationResult?> ProcessNextAsync(ActivationRequest request, string? agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var queued = await _messageQueue.TryClaimNextAsync(agentId, cancellationToken).ConfigureAwait(false);
        if (queued is null)
        {
            return null;
        }

        var message = queued.Message;
        if (!_drivers.TryGetValue(message.AgentId, out var driver))
        {
            var error = $"Agent driver '{message.AgentId}' is not registered.";
            await _messageQueue.MarkFailedAsync(message.MessageId, error, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            return new ActivationResult(message.MessageId, null, null, false, error);
        }

        await using var mutex = await _conversationMutex.AcquireAsync(message.ConversationId, message.AgentId, cancellationToken).ConfigureAwait(false);
        var runId = $"run-{Guid.NewGuid():N}";
        await _runs.QueueAsync(runId, message.MessageId, message.ConversationId, message.AgentId, cancellationToken).ConfigureAwait(false);
        await _runs.MarkStartingAsync(runId, cancellationToken).ConfigureAwait(false);
        var session = await _sessions.GetAsync(message.ConversationId, message.AgentId, cancellationToken).ConfigureAwait(false);
        var canResume = session is { Status: ConversationSessionStatus.Idle };

        try
        {
            if (canResume)
            {
                await _sessions.StartRunAsync(message.ConversationId, message.AgentId, cancellationToken).ConfigureAwait(false);
            }

            await _messageQueue.MarkRunningAsync(message.MessageId, cancellationToken).ConfigureAwait(false);
            var environment = CreateEnvironment(request, message.MessageId, message.ConversationId, message.AgentId);
            var driverRequest = new AgentDriverRequest(
                message.Body,
                request.WorkingDirectory,
                environment,
                (processId, token) => _runs.MarkRunningAsync(runId, processId, token));
            var result = canResume
                ? await driver.ResumeAsync(session!.NativeSessionId, driverRequest, cancellationToken).ConfigureAwait(false)
                : await driver.StartAsync(driverRequest, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result.FinalMessage))
            {
                throw new InvalidOperationException("Agent completed without a final message.");
            }

            var run = await _runs.GetAsync(runId, cancellationToken).ConfigureAwait(false);
            if (run?.Status == AgentRunStatus.Starting)
            {
                await _runs.MarkRunningAsync(runId, result.ProcessResult.ProcessId, cancellationToken).ConfigureAwait(false);
            }

            await _runs.CompleteAsync(runId, result.NativeSessionId, result.ProcessResult.ExitCode, result.FinalMessage, cancellationToken).ConfigureAwait(false);
            if (canResume)
            {
                await _sessions.CompleteRunAsync(message.ConversationId, message.AgentId, result.NativeSessionId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _sessions.RegisterAsync(message.ConversationId, message.AgentId, result.NativeSessionId, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var replyMessageId = await _itoguruma.ReplyAsync(
                    message.SenderAgentId,
                    result.FinalMessage,
                    message.ConversationId,
                    message.MessageId,
                    ReplyIdempotencyKey.Create(message.MessageId),
                    cancellationToken).ConfigureAwait(false);
                await _messageQueue.MarkRespondedAsync(message.MessageId, replyMessageId, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                return new ActivationResult(message.MessageId, runId, replyMessageId, true, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await _replyRetries.ScheduleInitialFailureAsync(
                    message.MessageId, exception.Message, _timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                return new ActivationResult(message.MessageId, runId, null, false, exception.Message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelRunAsync(runId, session, message.MessageId).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await FailRunAsync(runId, session, message.MessageId, exception).ConfigureAwait(false);
            return new ActivationResult(message.MessageId, runId, null, false, exception.Message);
        }
    }

    private async Task CancelRunAsync(string runId, ConversationSession? session, string messageId)
    {
        var run = await _runs.GetAsync(runId, CancellationToken.None).ConfigureAwait(false);
        if (run?.Status is AgentRunStatus.Queued or AgentRunStatus.Starting or AgentRunStatus.Running)
        {
            await _runs.CancelAsync(runId, CancellationToken.None).ConfigureAwait(false);
        }

        await InvalidateRunningSessionAsync(session).ConfigureAwait(false);
        await _messageQueue.MarkFailedAsync(messageId, "Activation was cancelled.", _timeProvider.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
    }

    private async Task FailRunAsync(string runId, ConversationSession? session, string messageId, Exception exception)
    {
        var run = await _runs.GetAsync(runId, CancellationToken.None).ConfigureAwait(false);
        if (run?.Status is AgentRunStatus.Starting or AgentRunStatus.Running)
        {
            int? exitCode = exception is AgentDriverException driverException ? driverException.ProcessResult.ExitCode : null;
            await _runs.FailAsync(runId, exitCode, exception.Message, CancellationToken.None).ConfigureAwait(false);
        }

        await InvalidateRunningSessionAsync(session).ConfigureAwait(false);
        await _messageQueue.MarkFailedAsync(messageId, exception.Message, _timeProvider.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
    }

    private async Task InvalidateRunningSessionAsync(ConversationSession? session)
    {
        if (session is null)
        {
            return;
        }

        var current = await _sessions.GetAsync(session.ConversationId, session.AgentId, CancellationToken.None).ConfigureAwait(false);
        if (current?.Status == ConversationSessionStatus.Running)
        {
            await _sessions.InvalidateAsync(current.ConversationId, current.AgentId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateEnvironment(ActivationRequest request, string messageId, string conversationId, string agentId) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HATAORI_ROOT"] = request.HataoriRoot,
            ["HATAORI_CONVERSATION_ID"] = conversationId,
            ["HATAORI_MESSAGE_ID"] = messageId,
            ["HATAORI_AGENT_ID"] = agentId,
            ["HATAORI_MCP_URL"] = request.McpUrl,
        };
}
