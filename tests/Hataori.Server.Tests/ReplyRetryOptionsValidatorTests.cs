using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ReplyRetryOptionsValidatorTests
{
    [Fact]
    public void Validate_Defaults_ReturnsSuccess()
    {
        new ReplyRetryOptionsValidator().Validate(null, new ReplyRetryOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaximumDelayBelowInitial_ReturnsFailure()
    {
        var options = new ReplyRetryOptions { InitialDelaySeconds = 10, MaximumDelaySeconds = 5 };

        new ReplyRetryOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }
}
