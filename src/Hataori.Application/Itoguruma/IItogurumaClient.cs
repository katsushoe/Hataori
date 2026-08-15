namespace Hataori.Application.Itoguruma;

/// <summary>
/// Itoguruma MCP APIをHataoriから利用する境界です。
/// </summary>
public interface IItogurumaClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ItogurumaMessage>> GetMessagesAsync(int limit, int leaseSeconds, string? threadId, CancellationToken cancellationToken);
    Task<string> ReplyAsync(string recipientAgentId, string body, string threadId, string? replyToMessageId, string idempotencyKey, CancellationToken cancellationToken);
    Task<bool> AcknowledgeAsync(string messageId, CancellationToken cancellationToken);
    Task<ItogurumaStatus> GetStatusAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Itogurumaからリースしたメッセージです。
/// </summary>
public sealed record ItogurumaMessage(
    string MessageId,
    string ThreadId,
    string SenderAgentId,
    string? ReplyToMessageId,
    string MessageType,
    string Body,
    string? PayloadJson,
    DateTimeOffset CreatedAt,
    string DeliveryStatus,
    DateTimeOffset? LeaseUntil);

/// <summary>
/// Itoguruma接続状態です。
/// </summary>
public sealed record ItogurumaStatus(bool Connected, string Name, string Version);
