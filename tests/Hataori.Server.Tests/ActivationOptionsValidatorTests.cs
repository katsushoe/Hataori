using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ActivationOptionsValidatorTests
{
    private readonly string _firstRoot = Path.Combine(Path.GetTempPath(), $"hataori-workspace-a-{Guid.NewGuid():N}");
    private readonly string _secondRoot = Path.Combine(Path.GetTempPath(), $"hataori-workspace-b-{Guid.NewGuid():N}");

    [Fact]
    public void Validate_DisabledWithoutWorkingDirectory_ReturnsSuccess()
    {
        var options = new ActivationOptions { ProviderPriority = ["codex", "claude-code"] };

        new ActivationOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_EnabledWithRelativeWorkingDirectory_ReturnsFailure()
    {
        var options = new ActivationOptions { Enabled = true, WorkingDirectory = "." };

        new ActivationOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_ZeroConcurrency_ReturnsFailure()
    {
        var options = new ActivationOptions { MaxConcurrentRuns = new Dictionary<string, int> { ["codex"] = 0 } };

        new ActivationOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_DuplicateProviderPriority_ReturnsFailure()
    {
        var options = new ActivationOptions { ProviderPriority = ["codex", "CODEX"] };

        new ActivationOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidWorkspaceId_ReturnsFailure()
    {
        var options = new ActivationOptions { WorkspaceId = "invalid-workspace" };

        new ActivationOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_MultipleDistinctWorkspaces_ReturnsSuccess()
    {
        Directory.CreateDirectory(Path.Combine(_firstRoot, "Hataori"));
        Directory.CreateDirectory(Path.Combine(_secondRoot, "Kotodama"));
        var options = CreateEnabledOptions(
            new ActivationWorkspaceOptions { WorkspaceId = "alpha", WorkingDirectory = _firstRoot },
            new ActivationWorkspaceOptions { WorkspaceId = "beta", WorkingDirectory = _secondRoot });

        var result = new ActivationOptionsValidator().Validate(null, options);

        result.Succeeded.Should().BeTrue();
        Directory.Delete(_firstRoot, true);
        Directory.Delete(_secondRoot, true);
    }

    [Fact]
    public void Validate_DuplicateProjectAcrossWorkspaces_ReturnsFailure()
    {
        Directory.CreateDirectory(Path.Combine(_firstRoot, "Hataori"));
        Directory.CreateDirectory(Path.Combine(_secondRoot, "HATAORI"));
        var options = CreateEnabledOptions(
            new ActivationWorkspaceOptions { WorkspaceId = "alpha", WorkingDirectory = _firstRoot },
            new ActivationWorkspaceOptions { WorkspaceId = "beta", WorkingDirectory = _secondRoot });

        var result = new ActivationOptionsValidator().Validate(null, options);

        result.Failures.Should().Contain(error => error.Contains("exists in more than one workspace", StringComparison.Ordinal));
        Directory.Delete(_firstRoot, true);
        Directory.Delete(_secondRoot, true);
    }

    private static ActivationOptions CreateEnabledOptions(params ActivationWorkspaceOptions[] workspaces) =>
        new()
        {
            Enabled = true,
            Workspaces = workspaces,
            ProviderPriority = ["codex"],
            MaxConcurrentRuns = new Dictionary<string, int> { ["codex"] = 1 },
        };
}
