using Hataori.Application.Agents;
using Hataori.Application.Runs;

namespace Hataori.Infrastructure.Agents.Codex;

/// <summary>
/// Codex CLIの非対話execを1ターン単位で実行します。
/// </summary>
public sealed class CodexDriver(IAgentProcessManager processManager, CodexDriverOptions options) : IAgentDriver
{
    public string AgentType => "codex";

    public Task<AgentDriverResult> StartAsync(AgentDriverRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(null, request, cancellationToken);

    public Task<AgentDriverResult> ResumeAsync(string nativeSessionId, AgentDriverRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeSessionId);
        return ExecuteAsync(nativeSessionId, request, cancellationToken);
    }

    private async Task<AgentDriverResult> ExecuteAsync(string? nativeSessionId, AgentDriverRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);
        var arguments = nativeSessionId is null
            ? CodexCommandBuilder.BuildStart(options, request.WorkingDirectory)
            : CodexCommandBuilder.BuildResume(options, nativeSessionId);
        var processRequest = new AgentProcessStartRequest(
            options.ExecutablePath,
            arguments,
            request.WorkingDirectory,
            request.Environment,
            options.MaxCapturedCharacters,
            request.Message);
        await using var process = await processManager.StartAsync(processRequest, cancellationToken).ConfigureAwait(false);
        if (request.ProcessStarted is not null)
        {
            await request.ProcessStarted(process.ProcessId, cancellationToken).ConfigureAwait(false);
        }

        var processResult = await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var parsed = CodexJsonlParser.Parse(processResult.StandardOutput);
        if (processResult.ExitCode != 0 || parsed.Error is not null)
        {
            var message = parsed.Error ?? FirstNonEmptyLine(processResult.StandardError) ?? $"Codex exited with code {processResult.ExitCode}.";
            throw new AgentDriverException(message, processResult);
        }

        var resolvedSessionId = parsed.NativeSessionId ?? nativeSessionId;
        if (string.IsNullOrWhiteSpace(resolvedSessionId))
        {
            throw new AgentDriverException("Codex output did not contain a thread ID.", processResult);
        }

        return new AgentDriverResult(resolvedSessionId, parsed.FinalMessage, processResult);
    }

    private static string? FirstNonEmptyLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
