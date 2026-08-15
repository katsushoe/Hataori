using Hataori.Application.Runs;

namespace Hataori.Application.Agents;

public interface IAgentDriver
{
    string AgentType { get; }
    Task<AgentDriverResult> StartAsync(AgentDriverRequest request, CancellationToken cancellationToken);
    Task<AgentDriverResult> ResumeAsync(string nativeSessionId, AgentDriverRequest request, CancellationToken cancellationToken);
}

public sealed record AgentDriverRequest(
    string Message,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);

public sealed record AgentDriverResult(
    string NativeSessionId,
    string? FinalMessage,
    AgentProcessResult ProcessResult);

public sealed class AgentDriverException(string message, AgentProcessResult processResult) : Exception(message)
{
    public AgentProcessResult ProcessResult { get; } = processResult;
}
