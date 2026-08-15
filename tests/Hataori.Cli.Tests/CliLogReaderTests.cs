using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class CliLogReaderTests : IDisposable
{
    private readonly string _directoryPath = Path.Combine(Path.GetTempPath(), $"hataori-cli-logs-{Guid.NewGuid():N}");

    [Fact]
    public async Task ReadAsync_FilterAndLimit_ReturnsLatestMatchingLines()
    {
        Directory.CreateDirectory(_directoryPath);
        await File.WriteAllLinesAsync(Path.Combine(_directoryPath, "hataori-20260816.log"),
        [
            "first AgentId=codex RunId=run-1",
            "second AgentId=claude-code RunId=run-2",
            "third AgentId=codex RunId=run-3",
        ]);

        var lines = await new CliLogReader().ReadAsync(_directoryPath, 1, "codex", null, CancellationToken.None);

        lines.Should().Equal("third AgentId=codex RunId=run-3");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, true);
        }
    }
}
