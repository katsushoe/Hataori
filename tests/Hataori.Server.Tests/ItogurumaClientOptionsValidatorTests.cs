using FluentAssertions;
using Hataori.Infrastructure.Itoguruma;

namespace Hataori.Server.Tests;

public sealed class ItogurumaClientOptionsValidatorTests
{
    [Fact]
    public void Validate_ValidLoopbackConfiguration_ReturnsSuccess()
    {
        var result = new ItogurumaClientOptionsValidator().Validate(null, CreateValidOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingAuthenticationToken_ReturnsFailure()
    {
        var options = CreateValidOptions(authenticationToken: string.Empty);

        var result = new ItogurumaClientOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("authentication token", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NonLoopbackEndpoint_ReturnsFailure()
    {
        var options = CreateValidOptions(endpoint: new Uri("https://itoguruma.example/mcp"));

        var result = new ItogurumaClientOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("loopback", StringComparison.Ordinal));
    }

    private static ItogurumaClientOptions CreateValidOptions(
        string authenticationToken = "test-token",
        Uri? endpoint = null) => new()
        {
            Endpoint = endpoint ?? new Uri("http://127.0.0.1:47631/mcp"),
            AuthenticationToken = authenticationToken,
            AgentId = "hataori",
            AgentType = "hataori",
            ConnectionTimeoutSeconds = 10,
            PollIntervalSeconds = 5,
            MaxReconnectAttempts = 5,
            ReceiveBatchSize = 50,
            LeaseSeconds = 300,
        };
}
