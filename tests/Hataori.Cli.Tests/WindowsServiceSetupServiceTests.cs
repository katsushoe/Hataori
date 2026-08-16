using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class WindowsServiceSetupServiceTests
{
    [Fact]
    public async Task ConfigureAsync_IssuedToken_WritesWithoutReturningSecret()
    {
        var environment = new FakeEnvironmentStore("secret-token");
        var store = new FakeCredentialStore();
        var service = new WindowsServiceSetupService(environment, store);

        var result = await service.ConfigureAsync(CancellationToken.None);

        result.Configured.Should().BeTrue();
        result.ConfigurationPath.Should().Be("service-config.json");
        store.Token.Should().Be("secret-token");
        result.ToString().Should().NotContain("secret-token");
    }

    [Fact]
    public async Task ConfigureAsync_MissingIssuedToken_ReturnsActionableError()
    {
        var service = new WindowsServiceSetupService(new FakeEnvironmentStore(null), new FakeCredentialStore());

        var action = () => service.ConfigureAsync(CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Install or repair Itoguruma*");
    }

    private sealed class FakeEnvironmentStore(string? token) : IEnvironmentVariableStore
    {
        public string? Get(string name, EnvironmentVariableTarget target) => token;
        public void Set(string name, string value, EnvironmentVariableTarget target) => throw new NotSupportedException();
    }

    private sealed class FakeCredentialStore : IWindowsServiceCredentialStore
    {
        public string? Token { get; private set; }

        public Task<string> WriteAuthenticationTokenAsync(string token, CancellationToken cancellationToken)
        {
            Token = token;
            return Task.FromResult("service-config.json");
        }
    }
}
