using FluentAssertions;
using Hataori.Application.Activation;

namespace Hataori.Server.Tests;

public sealed class ReplyIdempotencyKeyTests
{
    [Fact]
    public void Create_SameMessageId_ReturnsStableKey()
    {
        ReplyIdempotencyKey.Create("message-1").Should().Be("hataori-reply:message-1");
        ReplyIdempotencyKey.Create("message-1").Should().Be("hataori-reply:message-1");
    }
}
