using FluentAssertions;
using Hataori.Core.Sessions;

namespace Hataori.Core.Tests.Sessions;

public sealed class ConversationSessionTests
{
    [Fact]
    public void Lifecycle_StartCompleteInvalidate_TransitionsState()
    {
        var startedAt = DateTimeOffset.Parse("2026-08-15T00:00:00Z");
        var session = ConversationSession.Create("conversation-1", "codex", "session-1", startedAt);

        session.StartRun(startedAt.AddMinutes(1));
        session.CompleteRun("session-2", startedAt.AddMinutes(2));
        session.Invalidate(startedAt.AddMinutes(3));

        session.Status.Should().Be(ConversationSessionStatus.Invalid);
        session.NativeSessionId.Should().Be("session-2");
        session.InvalidatedAtUtc.Should().Be(startedAt.AddMinutes(3));
    }

    [Fact]
    public void StartRun_WhenAlreadyRunning_Throws()
    {
        var session = ConversationSession.Create("conversation-1", "codex", "session-1", DateTimeOffset.UtcNow);
        session.StartRun(DateTimeOffset.UtcNow);

        var action = () => session.StartRun(DateTimeOffset.UtcNow);

        action.Should().Throw<InvalidOperationException>();
    }
}
