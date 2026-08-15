using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class WindowsServiceManagerTests
{
    [Theory]
    [InlineData("start", "start")]
    [InlineData("stop", "stop")]
    [InlineData("status", "query")]
    [InlineData("uninstall", "delete")]
    public async Task ExecuteAsync_ServiceCommand_UsesScExecutable(string command, string expectedScCommand)
    {
        var runner = new FakeProcessRunner();
        var manager = new WindowsServiceManager(runner);

        await manager.ExecuteAsync(command, "Hataori-Test", null, CancellationToken.None);

        runner.FileName.Should().Be("sc.exe");
        runner.Arguments.Should().Equal(expectedScCommand, "Hataori-Test");
    }

    [Fact]
    public async Task ExecuteAsync_Restart_StopsThenStartsService()
    {
        var runner = new FakeProcessRunner();
        var manager = new WindowsServiceManager(runner);

        await manager.ExecuteAsync("restart", "Hataori-Test", null, CancellationToken.None);

        runner.Invocations.Should().HaveCount(2);
        runner.Invocations[0].Should().Equal("stop", "Hataori-Test");
        runner.Invocations[1].Should().Equal("start", "Hataori-Test");
    }

    [Fact]
    public async Task ExecuteAsync_InstallWithoutServerPath_ReturnsInvalidArguments()
    {
        var manager = new WindowsServiceManager(new FakeProcessRunner());

        var action = () => manager.ExecuteAsync("install", "Hataori-Test", null, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*--server*");
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string? FileName { get; private set; }
        public IReadOnlyList<string> Arguments { get; private set; } = [];
        public List<IReadOnlyList<string>> Invocations { get; } = [];

        public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            FileName = fileName;
            Arguments = arguments;
            Invocations.Add(arguments);
            return Task.FromResult(new ProcessRunResult(0, "ok", string.Empty));
        }
    }
}
