using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Hataori.Core.Messages;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Sessions;
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

    [Fact]
    public async Task RunAsync_AgentRuns_ReturnsPersistedRuns()
    {
        var repository = new SqliteAgentRunRepository(GetConnectionString());
        await repository.InitializeAsync(CancellationToken.None);
        await repository.AddAsync(AgentRun.Queue("run-1", "message-1", "conversation-1", "codex", DateTimeOffset.UtcNow), CancellationToken.None);

        var response = await RunAsync("agent", "runs", "--database", _databasePath, "--agent", "codex");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("run_id").GetString().Should().Be("run-1");
    }

    [Fact]
    public async Task RunAsync_ConversationList_ReturnsPersistedSessions()
    {
        var repository = new SqliteConversationSessionRepository(GetConnectionString());
        await repository.InitializeAsync(CancellationToken.None);
        await repository.SaveAsync(ConversationSession.Create("conversation-1", "codex", "session-1", DateTimeOffset.UtcNow), CancellationToken.None);

        var response = await RunAsync("conversation", "list", "--database", _databasePath, "--agent", "codex");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("conversation_id").GetString().Should().Be("conversation-1");
    }

    [Fact]
    public async Task RunAsync_QueueList_ReturnsQueuedMessages()
    {
        var repository = new SqliteMessageQueueRepository(GetConnectionString());
        await repository.InitializeAsync(CancellationToken.None);
        var message = new IncomingMessage("message-1", "conversation-1", "codex", "sender", null, "prompt", "body", null, DateTimeOffset.UtcNow);
        await repository.EnqueueAsync(message, 0, CancellationToken.None);

        var response = await RunAsync("queue", "list", "--database", _databasePath, "--agent", "codex");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("message").GetProperty("message_id").GetString().Should().Be("message-1");
    }

    [Fact]
    public async Task RunAsync_QueueGetAndCancel_AcceptsPositionalMessageId()
    {
        var repository = new SqliteMessageQueueRepository(GetConnectionString());
        await repository.InitializeAsync(CancellationToken.None);
        var message = new IncomingMessage("message-1", "conversation-1", "codex", "sender", null, "prompt", "body", null, DateTimeOffset.UtcNow);
        await repository.EnqueueAsync(message, 0, CancellationToken.None);

        var getResponse = await RunAsync("queue", "get", "message-1", "--database", _databasePath);
        var cancelResponse = await RunAsync("queue", "cancel", "message-1", "--database", _databasePath);

        getResponse.ExitCode.Should().Be(0);
        cancelResponse.ExitCode.Should().Be(0);
        (await repository.ListAsync(null, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ConversationReset_AcceptsPositionalConversationId()
    {
        var repository = new SqliteConversationSessionRepository(GetConnectionString());
        await repository.InitializeAsync(CancellationToken.None);
        await repository.SaveAsync(ConversationSession.Create("conversation-1", "codex", "session-1", DateTimeOffset.UtcNow), CancellationToken.None);

        var response = await RunAsync("conversation", "reset", "conversation-1", "--database", _databasePath, "--agent", "codex");

        response.ExitCode.Should().Be(0);
        var session = await repository.GetAsync("conversation-1", "codex", CancellationToken.None);
        session!.Status.Should().Be(ConversationSessionStatus.Invalid);
    }

    [Fact]
    public async Task RunAsync_DbIntegrity_ReturnsOk()
    {
        (await RunAsync("task", "list", "--database", _databasePath)).ExitCode.Should().Be(0);

        var response = await RunAsync("db", "integrity", "--database", _databasePath);

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_Version_ReturnsAssemblyVersion()
    {
        var response = await RunAsync("--version");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("version").GetString().Should().NotBeNullOrWhiteSpace();
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

    private string GetConnectionString() => new SqliteConnectionStringBuilder { DataSource = _databasePath, ForeignKeys = true }.ToString();

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
