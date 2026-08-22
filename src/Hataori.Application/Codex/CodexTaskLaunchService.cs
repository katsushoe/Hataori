using Hataori.Core.Codex;

namespace Hataori.Application.Codex;

/// <summary>Codex Desktop受信タスク向けのclaim・開始記録を提供します。</summary>
public sealed class CodexTaskLaunchService(ICodexTaskLaunchRepository repository, TimeProvider timeProvider)
{
    public async Task<CodexTaskLaunch?> ClaimAsync(int leaseSeconds, CancellationToken cancellationToken)
    {
        if (leaseSeconds is < 30 or > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseSeconds), "Lease seconds must be between 30 and 3600.");
        }

        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        return await repository.TryClaimAsync(now, now.AddSeconds(leaseSeconds), cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkStartedAsync(string messageId, string claimToken, string codexTaskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(codexTaskId);
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await repository.MarkStartedAsync(messageId, claimToken, codexTaskId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(string messageId, string claimToken, string error, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await repository.ReleaseAsync(messageId, claimToken, error, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }
}
