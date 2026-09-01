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

    [Fact]
    public void Bind_MultipleWorkspaces_PreservesEveryRoot()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["activation:workspaces:0:workspaceId"] = "alpha",
                ["activation:workspaces:0:workingDirectory"] = @"C:\ProjectsA",
                ["activation:workspaces:1:workspaceId"] = "beta",
                ["activation:workspaces:1:workingDirectory"] = @"C:\ProjectsB",
            })
            .Build();

        var options = new ActivationOptions();
        configuration.GetSection(ActivationOptions.SectionName).Bind(options);

        options.Workspaces.Should().HaveCount(2);
        options.Workspaces.Select(workspace => workspace.WorkspaceId).Should().Equal("alpha", "beta");
    }
}
