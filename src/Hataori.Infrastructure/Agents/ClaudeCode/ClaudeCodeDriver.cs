using Hataori.Application.Agents;
using Hataori.Application.Runs;

namespace Hataori.Infrastructure.Agents.ClaudeCode;

/// <summary>
/// Claude Code CLIのprint modeを1ターン単位で実行します。
/// </summary>
public sealed class ClaudeCodeDriver(IAgentProcessManager processManager, ClaudeCodeDriverOptions options) : IAgentDriver
{
    public string AgentType => "claude-code";

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
            ? ClaudeCodeCommandBuilder.BuildStart(options)
            : ClaudeCodeCommandBuilder.BuildResume(options, nativeSessionId);
        var processRequest = new AgentProcessStartRequest(
            options.ExecutablePath,
            arguments,
            request.WorkingDirectory,
            request.Environment,
            options.MaxCapturedCharacters,
            request.Message);
        await using var process = await processManager.StartAsync(processRequest, cancellationToken).ConfigureAwait(false);
        var processResult = await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (processResult.ExitCode != 0)
        {
            var message = FirstNonEmptyLine(processResult.StandardError) ?? $"Claude Code exited with code {processResult.ExitCode}.";
            throw new AgentDriverException(message, processResult);
        }

        var parsed = ClaudeCodeJsonParser.Parse(processResult.StandardOutput);
        if (parsed.Error is not null)
        {
            throw new AgentDriverException(parsed.Error, processResult);
        }

        var resolvedSessionId = parsed.NativeSessionId ?? nativeSessionId;
        if (string.IsNullOrWhiteSpace(resolvedSessionId))
        {
            throw new AgentDriverException("Claude Code output did not contain a session ID.", processResult);
        }

        return new AgentDriverResult(resolvedSessionId, parsed.FinalMessage, processResult);
    }

    private static string? FirstNonEmptyLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
}
