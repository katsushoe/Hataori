using FluentAssertions;
using Hataori.Application.Control;
using Microsoft.Extensions.Hosting;

namespace Hataori.Server.Tests;

public sealed class ServerFoundationTests
{
    [Fact]
    public void Validate_MissingDatabasePath_ReturnsFailure()
    {
        var result = new ServerOptionsValidator().Validate(null, new ServerOptions { ControlPipeName = "hataori-test" });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_NonLoopbackMcpHost_ReturnsFailure()
    {
        var options = new ServerOptions
        {
            DatabasePath = "data/hataori.db",
            ControlPipeName = "hataori-test",
            McpHost = "0.0.0.0",
            McpPort = 45440,
            McpPath = "/mcp",
        };

        var result = new ServerOptionsValidator().Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void ResolveDatabasePath_RelativePath_UsesApplicationDirectory()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "hataori-server-test");

        var result = ServerPaths.ResolveDatabasePath(Path.Combine("data", "hataori.db"), baseDirectory);

        result.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "data", "hataori.db")));
    }

    [Fact]
    public void Handle_Stop_RequestsGracefulShutdown()
    {
        var lifetime = new TestLifetime();
        var handler = new ControlCommandHandler(lifetime, TimeProvider.System);

        var response = handler.Handle(new ControlRequest("stop"));

        response.Success.Should().BeTrue();
        response.Status.Should().Be("stopping");
        lifetime.StopRequested.Should().BeTrue();
    }

    [Fact]
    public void Handle_Reload_ReportsAutomaticReload()
    {
        var lifetime = new TestLifetime();
        var handler = new ControlCommandHandler(lifetime, TimeProvider.System);

        var response = handler.Handle(new ControlRequest("reload"));

        response.Success.Should().BeTrue();
        response.Status.Should().Be("reload_on_change_enabled");
        lifetime.StopRequested.Should().BeFalse();
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public bool StopRequested { get; private set; }
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => StopRequested = true;
    }
}
