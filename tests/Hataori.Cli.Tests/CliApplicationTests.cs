using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace Hataori.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"hataori-cli-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task RunAsync_TaskLifecycle_PersistsAndReturnsJson()
    {
        (await RunAsync("task", "start", "--database", _databasePath, "--id", "task-1", "--name", "実装", "--agent", "codex")).ExitCode.Should().Be(0);
        (await RunAsync("task", "heartbeat", "--database", _databasePath, "--id", "task-1", "--current-work", "CLI実装", "--progress", "50")).ExitCode.Should().Be(0);
        (await RunAsync("task", "complete", "--database", _databasePath, "--id", "task-1", "--result", "成功")).ExitCode.Should().Be(0);

        var response = await RunAsync("task", "get", "--database", _databasePath, "--id", "task-1");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("status").GetString().Should().Be("completed");
        document.RootElement.GetProperty("progress_percent").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task RunAsync_MissingTask_ReturnsNotFound()
    {
        var response = await RunAsync("task", "get", "--database", _databasePath, "--id", "missing");

        response.ExitCode.Should().Be(4);
        response.Error.Should().Contain("not found");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static async Task<CliResponse> RunAsync(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApplication.RunAsync(args, output, error, CancellationToken.None);
        return new CliResponse(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliResponse(int ExitCode, string Output, string Error);
}
