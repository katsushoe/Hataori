using System.Text.RegularExpressions;

namespace Hataori.Application.Itoguruma;

/// <summary>
/// Itoguruma MCP APIをHataoriから利用する境界です。
/// </summary>
public interface IItogurumaClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>Itogurumaに登録済みのProjectを返します。</summary>
    Task<IReadOnlyList<ItogurumaProject>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>ProjectとそのProject Inboxを登録します。既存Projectでは表示名を更新します。</summary>
    Task RegisterProjectInboxAsync(string projectId, string displayName, CancellationToken cancellationToken);

    /// <summary>InboxのメッセージをHataoriの受信者IDでリースします。</summary>
    Task<IReadOnlyList<ItogurumaMessage>> GetMessagesAsync(string agentId, int limit, int leaseSeconds, string? threadId, CancellationToken cancellationToken);

    Task<string> ReplyAsync(string recipientAgentId, string provider, string body, string threadId, string? replyToMessageId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>リース時に受け取ったlease IDでメッセージを確認済みにします。</summary>
    Task<bool> AcknowledgeAsync(string agentId, string messageId, string leaseId, CancellationToken cancellationToken);

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
    string? Provider,
    DateTimeOffset CreatedAt,
    string DeliveryStatus,
    DateTimeOffset? LeaseUntil,
    string? LeaseId = null);

/// <summary>
/// Itogurumaに登録されたメッセージ宛先Projectです。
/// </summary>
public sealed record ItogurumaProject(string ProjectId, string DisplayName, string InboxAgentId, bool Enabled);

/// <summary>
/// Itoguruma接続状態です。
/// </summary>
public sealed record ItogurumaStatus(bool Connected, string Name, string Version);

/// <summary>
/// ItogurumaのProject ID規則です。
/// </summary>
public static partial class ItogurumaProjectId
{
    /// <summary>Project IDがItogurumaの正規形式（英小文字で始まり、英小文字・数字・アンダースコアのみ）か判定します。</summary>
    public static bool IsValid(string? projectId) => projectId is not null && Pattern().IsMatch(projectId);

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
