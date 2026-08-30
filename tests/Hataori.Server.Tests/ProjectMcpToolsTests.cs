using FluentAssertions;
using Hataori.Application.Activation;
using Hataori.Application.Agents;
using Microsoft.Extensions.Options;

namespace Hataori.Server.Tests;

public sealed class ProjectMcpToolsTests : IDisposable
{
    private readonly string _projectsRoot = Path.Combine(Path.GetTempPath(), $"hataori-projects-{Guid.NewGuid():N}");

    [Fact]
    public void List_Query_ReturnsMatchingNormalizedProjectIds()
    {
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Hataori"));
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Kotodama"));
        var tools = new ProjectMcpTools(
            new AgentProviderSelector(Array.Empty<IAgentDriver>()),
            Options.Create(new ActivationOptions { WorkingDirectory = _projectsRoot }));

        var projects = tools.List("HAT");

        projects.Should().Equal("hataori");
    }

    [Fact]
    public void Select_UnregisteredProject_ReturnsRegisteredCandidatesInError()
    {
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Hataori"));
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Kotodama"));
        var selector = new AgentProviderSelector(Array.Empty<IAgentDriver>());

        var action = () => selector.Select(_projectsRoot, "missing", null, []);

        action.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*Registered project candidates: hataori, kotodama.*");
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectsRoot))
        {
            Directory.Delete(_projectsRoot, true);
        }
    }
}
