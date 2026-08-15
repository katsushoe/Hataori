using FluentAssertions;
using Hataori.Infrastructure.Sessions;

namespace Hataori.Infrastructure.Tests.Sessions;

public sealed class ConversationMutexTests
{
    [Fact]
    public async Task AcquireAsync_SameKey_WaitsUntilFirstLeaseIsReleased()
    {
        var mutex = new ConversationMutex();
        var first = await mutex.AcquireAsync("conversation-1", "codex", CancellationToken.None);
        var secondTask = mutex.AcquireAsync("conversation-1", "codex", CancellationToken.None).AsTask();

        secondTask.IsCompleted.Should().BeFalse();
        await first.DisposeAsync();
        await using var second = await secondTask;

        second.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_DifferentKey_DoesNotWait()
    {
        var mutex = new ConversationMutex();
        await using var held = await mutex.AcquireAsync("conversation-1", "codex", CancellationToken.None);

        var other = mutex.AcquireAsync("conversation-2", "codex", CancellationToken.None).AsTask();

        other.IsCompletedSuccessfully.Should().BeTrue();
        await (await other).DisposeAsync();
    }
}
