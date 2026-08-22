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
        (await RunAsync("task", "heartbeat", "task-1", "--database", _databasePath, "--current-work", "CLI実装", "--progress", "50", "--message", "半分完了")).ExitCode.Should().Be(0);
        (await RunAsync("task", "complete", "task-1", "--database", _databasePath, "--message", "成功")).ExitCode.Should().Be(0);

        var response = await RunAsync("task", "get", "task-1", "--database", _databasePath, "--json");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("task").GetProperty("status").GetString().Should().Be("completed");
        document.RootElement.GetProperty("task").GetProperty("progress_percent").GetInt32().Should().Be(100);
        document.RootElement.GetProperty("history").EnumerateArray().Should().Contain(item => item.GetProperty("message").GetString() == "半分完了");
        document.RootElement.GetProperty("relations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_MissingTask_ReturnsNotFound()
    {
        var response = await RunAsync("task", "get", "missing", "--database", _databasePath);

        response.ExitCode.Should().Be(4);
        response.Error.Should().Contain("見つかりません");
    }

    [Fact]
    public async Task RunAsync_TaskList_DefaultsToActiveAndAllFlagIncludesCompleted()
    {
        (await RunAsync("task", "start", "--database", _databasePath, "--id", "active", "--name", "Active", "--agent", "codex")).ExitCode.Should().Be(0);
        (await RunAsync("task", "start", "--database", _databasePath, "--id", "done", "--name", "Done", "--agent", "codex")).ExitCode.Should().Be(0);
        (await RunAsync("task", "complete", "done", "--database", _databasePath, "--message", "完了")).ExitCode.Should().Be(0);

        var active = await RunAsync("task", "list", "--database", _databasePath);
        var all = await RunAsync("task", "list", "--database", _databasePath, "--all", "--json");

        using var activeDocument = JsonDocument.Parse(active.Output);
        using var allDocument = JsonDocument.Parse(all.Output);
        activeDocument.RootElement.GetArrayLength().Should().Be(1);
        activeDocument.RootElement[0].GetProperty("task_id").GetString().Should().Be("active");
        allDocument.RootElement.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_TaskFindConflicts_MatchesMcpBehavior()
    {
        (await RunAsync("task", "start", "--database", _databasePath, "--id", "other", "--name", "認証処理を修正", "--agent", "codex", "--summary", "ログイン周りの改修")).ExitCode.Should().Be(0);
        (await RunAsync("task", "start", "--database", _databasePath, "--id", "own", "--name", "認証仕様を更新", "--agent", "claude-code", "--summary", "認証文書")).ExitCode.Should().Be(0);

        var response = await RunAsync("task", "find-conflicts", "--database", _databasePath, "--name", "認証処理のバグ修正", "--summary", "ログイン画面", "--agent", "claude-code");

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("task_id").GetString().Should().Be("other");
    }

    [Fact]
    public async Task RunAsync_ProviderPrioritySetAndGet_UsesConfigurationService()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hataori-cli-provider-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """{"activation":{"providerPriority":["codex"]}}""");

            var set = await RunAsync("provider", "priority", "set", "--providers", "claude-code,codex", "--config", path);
            var get = await RunAsync("provider", "priority", "get", "--config", path);

            set.ExitCode.Should().Be(0);
            get.ExitCode.Should().Be(0);
            using var document = JsonDocument.Parse(get.Output);
            document.RootElement.GetProperty("providers").EnumerateArray().Select(item => item.GetString())
                .Should().Equal("claude-code", "codex");
        }
        finally
        {
            File.Delete(path);
        }
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
    public async Task RunAsync_AgentCancel_SendsRunIdAsControlPipeArgument()
    {
        var pipeName = $"hataori-cli-test-{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var serverTask = RespondToAgentCancelAsync(server);

        var response = await RunAsync("agent", "cancel", "run-1", "--pipe", pipeName, "--timeout-seconds", "5");
        await serverTask;

        response.ExitCode.Should().Be(0);
        using var document = JsonDocument.Parse(response.Output);
        document.RootElement.GetProperty("run_id").GetString().Should().Be("run-1");
        document.RootElement.GetProperty("status").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task RunAsync_DoctorItoguruma_ReflectsLiveServerConnectionStateNotCliSideReconnect()
    {
        var pipeName = $"hataori-cli-test-{Guid.NewGuid():N}";
        var configPath = Path.Combine(Path.GetTempPath(), $"hataori-doctor-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(configPath, $$"""
            {
              "server": {
                "databasePath": "{{Path.GetTempPath().Replace("\\", "\\\\")}}hataori-doctor-test-{{Guid.NewGuid():N}}.db",
                "controlPipeName": "{{pipeName}}",
                "mcpHost": "127.0.0.1",
                "mcpPort": 45999,
                "mcpPath": "/mcp"
              }
            }
            """);
        var serverTask = RespondToStatusThenMonitorAsync(pipeName, itogurumaState: "degraded");

        var response = await RunAsync("doctor", "--config", configPath, "--timeout-seconds", "5");
        await serverTask;

        using var document = JsonDocument.Parse(response.Output);
        var itogurumaCheck = document.RootElement.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "itoguruma");
        itogurumaCheck.GetProperty("ok").GetBoolean().Should().BeFalse();
        itogurumaCheck.GetProperty("error").GetString().Should().Contain("degraded");
        File.Delete(configPath);
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
        var message = new IncomingMessage("message-1", "conversation-1", "codex", Directory.GetCurrentDirectory(), "sender", null, "prompt", "body", null, DateTimeOffset.UtcNow);
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
        var message = new IncomingMessage("message-1", "conversation-1", "codex", Directory.GetCurrentDirectory(), "sender", null, "prompt", "body", null, DateTimeOffset.UtcNow);
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

    [Fact]
    public async Task RunAsync_TaskHelp_ReturnsWithoutDatabaseConfiguration()
    {
        var response = await RunAsync("task", "--help");

        response.ExitCode.Should().Be(0);
        response.Output.Should().Contain("hataori task");
    }

    [Fact]
    public async Task RunAsync_Help_IncludesSetupCommand()
    {
        var response = await RunAsync("--help");

        response.ExitCode.Should().Be(0);
        response.Output.Should().Contain("setup");
    }

    [Fact]
    public void ItogurumaSetup_ExistingUserToken_LinksWithoutReturningSecret()
    {
        var environment = new TestEnvironmentVariableStore();
        environment.Set(ItogurumaSetupService.SourceVariable, "secret-token", EnvironmentVariableTarget.User);

        var result = new ItogurumaSetupService(environment).Configure();

        result.Configured.Should().BeTrue();
        result.ToString().Should().NotContain("secret-token");
        environment.Get(ItogurumaSetupService.TargetVariable, EnvironmentVariableTarget.User).Should().Be("secret-token");
        environment.Get(ItogurumaSetupService.TargetVariable, EnvironmentVariableTarget.Process).Should().Be("secret-token");
    }

    [Fact]
    public void ItogurumaSetup_MissingUserToken_ExplainsAutomaticIssuance()
    {
        var action = () => new ItogurumaSetupService(new TestEnvironmentVariableStore()).Configure();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Install or repair Itoguruma*");
    }

    [Fact]
    public async Task RunAsync_MonitorMissingExecutable_ReturnsDependencyError()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"hataori-monitor-{Guid.NewGuid():N}.exe");

        var response = await RunAsync("monitor", "--monitor", missingPath);

        response.ExitCode.Should().Be(3);
        response.Error.Should().Contain("見つかりません");
    }

    [Fact]
    public async Task RunAsync_ConfigInit_CreatesDefaultConfiguration()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"hataori-config-init-{Guid.NewGuid():N}");
        var configurationPath = Path.Combine(directoryPath, "hataori.json");
        try
        {
            var response = await RunAsync("config", "init", "--config", configurationPath);

            response.ExitCode.Should().Be(0);
            File.Exists(configurationPath).Should().BeTrue();
            response.Output.Should().Contain("\"created\": true");
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
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

    private static async Task RespondToStatusThenMonitorAsync(string pipeName, string itogurumaState)
    {
        for (var i = 0; i < 2; i++)
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(CancellationToken.None);
            using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
            await using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            var request = await reader.ReadLineAsync(CancellationToken.None);
            if (request!.Contains("monitor", StringComparison.OrdinalIgnoreCase))
            {
                var body = """{"success":true,"status":"running","timestamp_utc":"2026-08-15T00:00:00+00:00","monitor":{"tasks":[],"agents":[],"sessions":[],"runs":[],"queue_count":0,"system":{"server":"running","itoguruma":"__ITOGURUMA_STATE__","mcp":"running","sqlite":"connected"}}}"""
                    .Replace("__ITOGURUMA_STATE__", itogurumaState);
                await writer.WriteLineAsync(body);
            }
            else
            {
                await writer.WriteLineAsync("""{"success":true,"status":"running","timestamp_utc":"2026-08-15T00:00:00+00:00"}""");
            }
        }
    }

    private static async Task RespondToAgentCancelAsync(NamedPipeServerStream server)
    {
        await server.WaitForConnectionAsync(CancellationToken.None);
        using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
        await using var writer = new StreamWriter(server, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        var request = await reader.ReadLineAsync(CancellationToken.None);
        request.Should().Contain("agent-cancel");
        request.Should().Contain("run-1");
        await writer.WriteLineAsync("{\"success\":true,\"status\":\"cancelled\",\"timestamp_utc\":\"2026-08-15T00:00:00+00:00\"}");
    }

    private sealed record CliResponse(int ExitCode, string Output, string Error);

    private sealed class TestEnvironmentVariableStore : IEnvironmentVariableStore
    {
        private readonly Dictionary<(string Name, EnvironmentVariableTarget Target), string> _values = [];

        public string? Get(string name, EnvironmentVariableTarget target) =>
            _values.TryGetValue((name, target), out var value) ? value : null;

        public void Set(string name, string value, EnvironmentVariableTarget target) =>
            _values[(name, target)] = value;
    }
}
