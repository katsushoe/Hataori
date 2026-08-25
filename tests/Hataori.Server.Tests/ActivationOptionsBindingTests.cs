using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Hataori.Server.Tests;

public sealed class ActivationOptionsBindingTests
{
    [Fact]
    public void Bind_DefaultGeneratedProviderPriority_DoesNotDuplicate()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["activation:providerPriority:0"] = "codex",
                ["activation:providerPriority:1"] = "claude-code",
            })
            .Build();

        var options = new ActivationOptions();
        configuration.GetSection(ActivationOptions.SectionName).Bind(options);

        options.ProviderPriority.Should().Equal("codex", "claude-code");
    }

    [Fact]
    public void Bind_MissingProviderPriority_LeavesEmptyForCallerFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["activation:enabled"] = "false" })
            .Build();

        var options = new ActivationOptions();
        configuration.GetSection(ActivationOptions.SectionName).Bind(options);

        options.ProviderPriority.Should().BeEmpty();
    }
}
