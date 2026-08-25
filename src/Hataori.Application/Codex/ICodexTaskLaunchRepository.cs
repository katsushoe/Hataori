using Hataori.Core.Codex;

namespace Hataori.Application.Codex;

/// <summary>Codex Desktopタスク起動要求を永続化します。</summary>
public interface ICodexTaskLaunchRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<CodexTaskLaunch?> TryClaimAsync(DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken);
    Task MarkStartedAsync(string messageId, string claimToken, string codexTaskId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken);
    Task ReleaseAsync(string messageId, string claimToken, string error, DateTimeOffset releasedAtUtc, CancellationToken cancellationToken);
}
