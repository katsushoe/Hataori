using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Agents;
using NSubstitute;

namespace Hataori.Server.Tests;

public sealed class AgentProviderSelectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hataori-provider-{Guid.NewGuid():N}");

    [Fact]
    public void Select_SourceProviderAvailable_SelectsSourceProviderAndProjectDirectory()
    {
        var project = Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var selector = new AgentProviderSelector([CreateDriver("codex"), CreateDriver("claude-code")]);

        var result = selector.Select(_root, "hataori", "claude-code", ["codex", "claude-code"]);

        result.Should().Be(new ProviderSelection("claude-code", project.FullName));
    }

    [Fact]
    public void Select_SourceProviderUnavailable_SelectsFirstAvailableConfiguredProvider()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Hataori"));
        var selector = new AgentProviderSelector([CreateDriver("codex")]);

        var result = selector.Select(_root, "Hataori", "claude-code", ["codex", "claude-code"]);

        result.Provider.Should().Be("codex");
    }

    [Fact]
    public void Select_ProjectMissing_ThrowsDirectoryNotFoundException()
    {
        Directory.CreateDirectory(_root);
        var selector = new AgentProviderSelector([CreateDriver("codex")]);

        var action = () => selector.Select(_root, "missing", "codex", ["codex"]);

        action.Should().Throw<DirectoryNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
        GC.SuppressFinalize(this);
    }

    private static IAgentDriver CreateDriver(string provider)
    {
        var driver = Substitute.For<IAgentDriver>();
        driver.AgentType.Returns(provider);
        return driver;
    }
}
