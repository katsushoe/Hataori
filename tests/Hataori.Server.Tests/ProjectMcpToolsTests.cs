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

    [Fact]
    public void ListWorkspaces_ConfiguredRoot_ReturnsWorkspaceAndProjects()
    {
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Hataori"));
        var tools = new ProjectMcpTools(
            new AgentProviderSelector(Array.Empty<IAgentDriver>()),
            Options.Create(new ActivationOptions { WorkspaceId = "Main", WorkingDirectory = _projectsRoot }));

        var workspace = tools.ListWorkspaces().Should().ContainSingle().Subject;

        workspace.WorkspaceId.Should().Be("main");
        workspace.WorkingDirectory.Should().Be(Path.GetFullPath(_projectsRoot));
        workspace.ProjectIds.Should().Equal("hataori");
    }

    [Fact]
    public void List_NoConfiguredRoot_ReturnsEmpty()
    {
        var tools = new ProjectMcpTools(
            new AgentProviderSelector(Array.Empty<IAgentDriver>()),
            Options.Create(new ActivationOptions()));

        tools.List(null).Should().BeEmpty();
    }

    [Fact]
    public void ListWorkspaces_MultipleRoots_ReturnsEachWorkspace()
    {
        var secondRoot = Path.Combine(Path.GetTempPath(), $"hataori-projects-second-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_projectsRoot, "Hataori"));
        Directory.CreateDirectory(Path.Combine(secondRoot, "Kotodama"));
        var tools = new ProjectMcpTools(
            new AgentProviderSelector(Array.Empty<IAgentDriver>()),
            Options.Create(new ActivationOptions
            {
                Workspaces =
                [
                    new ActivationWorkspaceOptions { WorkspaceId = "alpha", WorkingDirectory = _projectsRoot },
                    new ActivationWorkspaceOptions { WorkspaceId = "beta", WorkingDirectory = secondRoot },
                ],
            }));

        var workspaces = tools.ListWorkspaces();

        workspaces.Select(workspace => workspace.WorkspaceId).Should().Equal("alpha", "beta");
        tools.List(null).Should().Equal("hataori", "kotodama");
        Directory.Delete(secondRoot, true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectsRoot))
        {
            Directory.Delete(_projectsRoot, true);
        }
    }
}
