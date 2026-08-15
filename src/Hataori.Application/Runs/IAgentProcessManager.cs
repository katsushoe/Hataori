namespace Hataori.Application.Runs;

public interface IAgentProcessManager
{
    Task<IAgentProcess> StartAsync(AgentProcessStartRequest request, CancellationToken cancellationToken);
}

public interface IAgentProcess : IAsyncDisposable
{
    int ProcessId { get; }
    Task<AgentProcessResult> WaitForExitAsync(CancellationToken cancellationToken);
    Task CancelAsync(CancellationToken cancellationToken);
}

public sealed record AgentProcessStartRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment,
    int MaxCapturedCharacters = 4 * 1024 * 1024);

public sealed record AgentProcessResult(
    int ProcessId,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc);
