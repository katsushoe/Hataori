using Hataori.Application.Sessions;

namespace Hataori.Infrastructure.Sessions;

/// <summary>
/// ConversationとAgentの組み合わせごとに実行を直列化します。
/// </summary>
public sealed class ConversationMutex : IConversationMutex
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string ConversationId, string AgentId), Entry> _entries = [];

    public async ValueTask<IAsyncDisposable> AcquireAsync(string conversationId, string agentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var key = (conversationId, agentId);
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(this, key, entry);
        }
        catch
        {
            RemoveReference(key, entry);
            throw;
        }
    }

    private void Release((string ConversationId, string AgentId) key, Entry entry)
    {
        entry.Semaphore.Release();
        RemoveReference(key, entry);
    }

    private void RemoveReference((string ConversationId, string AgentId) key, Entry entry)
    {
        lock (_gate)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0)
            {
                _entries.Remove(key);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser(ConversationMutex owner, (string ConversationId, string AgentId) key, Entry entry) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                owner.Release(key, entry);
            }

            return ValueTask.CompletedTask;
        }
    }
}
