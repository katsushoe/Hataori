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
    public void Validate_MissingAuthenticationToken_ReturnsSuccess()
    {
        // 未連携のItoguruma tokenは起動を止める理由にしない。ItogurumaConnectionWorkerがdegraded状態として扱う。
        var options = CreateValidOptions(authenticationToken: string.Empty);

        var result = new ItogurumaClientOptionsValidator().Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonLoopbackEndpoint_ReturnsFailure()
    {
        var options = CreateValidOptions(endpoint: new Uri("https://itoguruma.example/mcp"));

        var result = new ItogurumaClientOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("loopback", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Hataori")]
    [InlineData("aloe-lang")]
    [InlineData("")]
    public void Validate_ProjectIdOutsideItogurumaRule_ReturnsFailure(string projectId)
    {
        var options = CreateValidOptions();
        options = new ItogurumaClientOptions
        {
            Endpoint = options.Endpoint,
            AuthenticationToken = options.AuthenticationToken,
            AgentId = options.AgentId,
            AgentType = options.AgentType,
            ProjectId = projectId,
            ConnectionTimeoutSeconds = options.ConnectionTimeoutSeconds,
            PollIntervalSeconds = options.PollIntervalSeconds,
            MaxReconnectAttempts = options.MaxReconnectAttempts,
        };

        var result = new ItogurumaClientOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(message => message.Contains("projectId", StringComparison.Ordinal));
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
