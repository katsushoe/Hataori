using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class DoctorCheckRunnerTests
{
    [Fact]
    public async Task RunAsync_UnauthorizedAcrossServiceAccount_ReturnsSkippedResult()
    {
        var result = await DoctorCheckRunner.RunAsync(
            "server",
            () => throw new UnauthorizedAccessException("Access to the path is denied."),
            exception => exception is UnauthorizedAccessException);

        result.Ok.Should().BeFalse();
        result.Skipped.Should().BeTrue();
        result.Error.Should().Contain("same account as the Hataori Service").And.Contain("Access to the path is denied.");
    }

    [Fact]
    public async Task RunAsync_OrdinaryFailure_ReturnsFailedResult()
    {
        var result = await DoctorCheckRunner.RunAsync(
            "server",
            () => throw new IOException("Pipe failed."),
            exception => exception is UnauthorizedAccessException);

        result.Ok.Should().BeFalse();
        result.Skipped.Should().BeFalse();
        result.Error.Should().Be("Pipe failed.");
    }

    [Fact]
    public async Task RunAsync_Cancellation_PropagatesCancellation()
    {
        var action = () => DoctorCheckRunner.RunAsync("server", () => throw new OperationCanceledException());

        await action.Should().ThrowAsync<OperationCanceledException>();
    }
}
