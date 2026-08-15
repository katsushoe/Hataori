namespace Hataori.Infrastructure.Itoguruma;

/// <summary>
/// Itoguruma MCP Clientの接続設定です。
/// </summary>
public sealed class ItogurumaClientOptions
{
    public const string SectionName = "itoguruma";

    public Uri? Endpoint { get; init; }
    public string AuthenticationToken { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string AgentType { get; init; } = string.Empty;
    public IReadOnlyList<string> MonitoredAgentIds { get; init; } = [];
    public int ConnectionTimeoutSeconds { get; init; }
    public int PollIntervalSeconds { get; init; }
    public int MaxReconnectAttempts { get; init; }
    public int ReceiveBatchSize { get; init; } = 50;
    public int LeaseSeconds { get; init; } = 300;
}
