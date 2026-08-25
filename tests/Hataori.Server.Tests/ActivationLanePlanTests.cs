using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ActivationLanePlanTests
{
    [Fact]
    public void Create_PerAgentLimits_ReturnsOneLanePerConcurrencySlot()
    {
        var limits = new Dictionary<string, int> { ["codex"] = 2, ["claude-code"] = 3 };

        var lanes = ActivationLanePlan.Create(limits);

        lanes.Should().NotContain("codex");
        lanes.Count(agent => agent == "claude-code").Should().Be(3);
    }
}
