using System.IO.Pipes;
using System.Text;
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

    [Fact]
    public async Task RunAsync_ServerStatus_UsesNamedPipeAndReturnsJson()
    {
        var pipeName = $"hataori-cli-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var serverTask = RespondToStatusAsync(server);

        var response = await RunAsync("status", "--pipe", pipeName, "--timeout-seconds", "5");
        await serverTask;

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("status").GetString().Should().Be("running");
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

    private static async Task RespondToStatusAsync(NamedPipeServerStream server)
    {
        await server.WaitForConnectionAsync(CancellationToken.None);
        using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
        await using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var request = await reader.ReadLineAsync(CancellationToken.None);
        request.Should().Contain("status");
        await writer.WriteLineAsync("{\"success\":true,\"status\":\"running\",\"timestamp_utc\":\"2026-08-15T00:00:00+00:00\"}");
    }

    private sealed record CliResponse(int ExitCode, string Output, string Error);
}
