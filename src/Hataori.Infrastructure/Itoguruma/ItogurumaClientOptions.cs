namespace Hataori.Infrastructure.Itoguruma;

/// <summary>
/// Itoguruma MCP Clientの接続設定です。
/// </summary>
public sealed class ItogurumaClientOptions
{
    public const string SectionName = "itoguruma";

    public Uri? Endpoint { get; init; }
    public string AuthenticationToken { get; set; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string AgentType { get; init; } = string.Empty;

    /// <summary>Hataori自身が属するItoguruma Project IDです。agentIdと同じ場合はProject Inboxを受信者・送信者として使います。</summary>
    public string ProjectId { get; init; } = "hataori";
    public int ConnectionTimeoutSeconds { get; init; }
    public int PollIntervalSeconds { get; init; }
    public int MaxReconnectAttempts { get; init; }
    public int ReceiveBatchSize { get; init; } = 50;
    public int LeaseSeconds { get; init; } = 300;
}
