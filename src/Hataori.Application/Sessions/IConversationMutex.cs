namespace Hataori.Application.Sessions;

public interface IConversationMutex
{
    ValueTask<IAsyncDisposable> AcquireAsync(string conversationId, string agentId, CancellationToken cancellationToken);
}
