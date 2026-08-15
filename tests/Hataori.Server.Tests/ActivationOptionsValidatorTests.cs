using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ActivationOptionsValidatorTests
{
    [Fact]
    public void Validate_DisabledWithoutWorkingDirectory_ReturnsSuccess()
    {
        new ActivationOptionsValidator().Validate(null, new ActivationOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_EnabledWithRelativeWorkingDirectory_ReturnsFailure()
    {
        var options = new ActivationOptions { Enabled = true, WorkingDirectory = "." };

        new ActivationOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }
}
