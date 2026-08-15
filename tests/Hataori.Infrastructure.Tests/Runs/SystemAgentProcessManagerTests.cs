using FluentAssertions;
using Hataori.Application.Runs;
using Hataori.Infrastructure.Runs;

namespace Hataori.Infrastructure.Tests.Runs;

public sealed class SystemAgentProcessManagerTests
{
    [Fact]
    public async Task StartAsync_DotnetVersion_CapturesSuccessfulExit()
    {
        var manager = new SystemAgentProcessManager(TimeProvider.System);
        var request = new AgentProcessStartRequest(
            "dotnet", ["--version"], Directory.GetCurrentDirectory(),
            new Dictionary<string, string?>(), 1024);

        await using var process = await manager.StartAsync(request, CancellationToken.None);
        var result = await process.WaitForExitAsync(CancellationToken.None);

        result.ProcessId.Should().BeGreaterThan(0);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
        result.StandardError.Should().BeEmpty();
        result.StandardOutputTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_SmallCaptureLimit_TruncatesAndDrainsOutput()
    {
        var manager = new SystemAgentProcessManager(TimeProvider.System);
        var request = new AgentProcessStartRequest(
            "dotnet", ["--info"], Directory.GetCurrentDirectory(),
            new Dictionary<string, string?>(), 10);

        await using var process = await manager.StartAsync(request, CancellationToken.None);
        var result = await process.WaitForExitAsync(CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().HaveLength(10);
        result.StandardOutputTruncated.Should().BeTrue();
    }
}
