using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application;
using Hataori.Application.Control;
using Hataori.Application.Sessions;
using Hataori.Application.Tasks;
using Hataori.Core.Runs;
using Hataori.Core.Sessions;
using Hataori.Core.Tasks;
using Hataori.Infrastructure.Agents.ClaudeCode;
using Hataori.Infrastructure.Agents.Codex;
using Hataori.Infrastructure.Messages;
using Hataori.Infrastructure.Runs;
using Hataori.Infrastructure.Sessions;
using Hataori.Infrastructure.Tasks;
using Hataori.Infrastructure.Itoguruma;
using Hataori.Server;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hataori.Cli;

/// <summary>
/// Hataori管理CLIのコマンドを実行します。
/// </summary>
public static class CliApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>
    /// 指定されたCLI引数を実行し、仕様で定義された終了コードを返します。
    /// </summary>
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        return await RunAsync(args, TextReader.Null, output, error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>標準入力を使用するHook対応CLIコマンドを実行します。</summary>
    public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (args.Length == 0)
            {
                throw new ArgumentException(GetHelpText());
            }

            object? result;
            if (IsHelpCommand(args))
            {
                result = new { help = GetHelpText() };
            }
            else if (IsVersionCommand(args))
            {
                result = new { version = GetVersion() };
            }
            else if (IsSubcommandHelp(args))
            {
                result = new { help = GetSubcommandHelp(args[0]) };
            }
            else if (string.Equals(args[0], "task", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException("Usage: hataori task <command> [options]");
                }

                var hasPositional = args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal);
                var positional = hasPositional ? args[2] : null;
                var options = ParseOptions(args.Skip(hasPositional ? 3 : 2));
                var connectionString = new SqliteConnectionStringBuilder { DataSource = GetDatabasePath(options), ForeignKeys = true }.ToString();
                var repository = new SqliteTaskRepository(connectionString);
                await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
                result = await ExecuteTaskAsync(args[1], positional, options, new TaskService(repository, TimeProvider.System), cancellationToken).ConfigureAwait(false);
            }
            else if (IsDatabaseCommand(args[0]))
            {
                result = await ExecuteDatabaseCommandAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "service", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteServiceAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "config", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteConfigAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "setup", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteSetupAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "itoguruma", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteItogurumaAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "mcp", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteMcpAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteDoctorAsync(args, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "logs", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteLogsAsync(args, output, cancellationToken).ConfigureAwait(false);
            }
            else if (string.Equals(args[0], "monitor", StringComparison.OrdinalIgnoreCase))
            {
                result = ExecuteMonitor(args);
            }
            else if (string.Equals(args[0], "hook", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteHookAsync(args, input, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                result = await ExecuteServerAsync(args[0], ParseOptions(args.Skip(1)), cancellationToken).ConfigureAwait(false);
            }

            if (result is not null)
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions)).ConfigureAwait(false);
            }

            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 2;
        }
        catch (KeyNotFoundException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 4;
        }
        catch (FileNotFoundException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 3;
        }
        catch (TimeoutException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 3;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 5;
        }
        catch (IOException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 6;
        }
        catch (SqliteException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 9;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<object> ExecuteHookAsync(string[] args, TextReader input, CancellationToken cancellationToken)
    {
        var options = ParseOptions(args.Skip(1));
        var json = await input.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Hook command requires JSON on standard input.");
        }

        using var document = JsonDocument.Parse(json);
        var response = await new ControlPipeClient().SendAsync(GetPipeName(options), "monitor", GetControlTimeout(options), cancellationToken).ConfigureAwait(false);
        var snapshot = response.Monitor;

        return HookProcessor.Process(
            document.RootElement,
            snapshot,
            Environment.GetEnvironmentVariable("HATAORI_CONVERSATION_ID"),
            Environment.GetEnvironmentVariable("HATAORI_AGENT_ID"),
            Environment.GetEnvironmentVariable("HATAORI_MESSAGE_ID"),
            Environment.GetEnvironmentVariable("HATAORI_MCP_URL"));
    }

    private static object ExecuteMonitor(string[] args)
    {
        var options = ParseOptions(args.Skip(1));
        var configuredPath = Optional(options, "monitor") ?? Environment.GetEnvironmentVariable("HATAORI_MONITOR_PATH");
        var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(layout.BinPath, "monitor", "Hataori.Monitor.exe")
            : Path.GetFullPath(configuredPath);
        if (!File.Exists(path) && string.IsNullOrWhiteSpace(configuredPath))
        {
            path = Path.Combine(AppContext.BaseDirectory, "Hataori.Monitor.exe");
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Hataori Monitor was not found at '{path}'.", path);
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true };
        var pipe = Optional(options, "pipe");
        if (!string.IsNullOrWhiteSpace(pipe))
        {
            startInfo.ArgumentList.Add("--pipe");
            startInfo.ArgumentList.Add(pipe);
        }

        System.Diagnostics.Process.Start(startInfo)?.Dispose();
        return new { status = "started", path };
    }

    private static async Task<object?> ExecuteLogsAsync(string[] args, TextWriter output, CancellationToken cancellationToken)
    {
        var options = ParseOptions(args.Skip(1));
        var configuration = LoadConfiguration(options);
        var fileLogging = configuration.GetRequiredSection(FileLogOptions.SectionName).Get<FileLogOptions>()
            ?? throw new InvalidOperationException("File logging configuration is missing.");
        var directoryPath = Optional(options, "log-directory") ?? (Path.IsPathFullyQualified(fileLogging.DirectoryPath)
            ? Path.GetFullPath(fileLogging.DirectoryPath)
            : Path.GetFullPath(Path.Combine(InstallationLayout.Resolve(AppContext.BaseDirectory).RootPath, fileLogging.DirectoryPath)));
        var lineCount = ParseLineCount(Optional(options, "lines"));
        var reader = new CliLogReader();
        if (options.ContainsKey("follow"))
        {
            await reader.FollowAsync(directoryPath, lineCount, Optional(options, "agent"), Optional(options, "run"), output, cancellationToken).ConfigureAwait(false);
            return null;
        }

        var lines = await reader.ReadAsync(directoryPath, lineCount, Optional(options, "agent"), Optional(options, "run"), cancellationToken).ConfigureAwait(false);
        return new { directory_path = directoryPath, lines };
    }

    private static async Task<object> ExecuteItogurumaAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2 || (!args[1].Equals("status", StringComparison.OrdinalIgnoreCase) && !args[1].Equals("test", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Usage: hataori itoguruma <status|test> [options]");
        }

        var options = ParseOptions(args.Skip(2));
        var configuration = LoadConfiguration(options);
        var clientOptions = configuration.GetRequiredSection(ItogurumaClientOptions.SectionName).Get<ItogurumaClientOptions>()
            ?? throw new InvalidOperationException("Itoguruma configuration is missing.");
        await using var client = new McpItogurumaClient(clientOptions, NullLoggerFactory.Instance);
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        var status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new { status.Connected, status.Name, status.Version, tested = args[1].Equals("test", StringComparison.OrdinalIgnoreCase) };
    }

    private static async Task<object> ExecuteSetupAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2 || !args[1].Equals("itoguruma", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Usage: hataori setup itoguruma [--config <path>] [--skip-test]");
        }

        var options = ParseOptions(args.Skip(2));
        var setup = new ItogurumaSetupService(new SystemEnvironmentVariableStore()).Configure();
        if (options.ContainsKey("skip-test"))
        {
            return new
            {
                setup.Configured,
                setup.SourceVariable,
                setup.TargetVariable,
                connection_tested = false,
                restart_required = true,
                next_action = "Restart Hataori Server, then run 'hataori itoguruma test'.",
            };
        }

        try
        {
            var connection = await ExecuteItogurumaAsync(
                ["itoguruma", "test", "--config", GetConfigurationPath(options)],
                cancellationToken).ConfigureAwait(false);
            return new
            {
                setup.Configured,
                setup.SourceVariable,
                setup.TargetVariable,
                connection_tested = true,
                connection,
                restart_required = true,
                next_action = "Restart Hataori Server to load the linked token.",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "The token was linked without displaying it, but the connection test failed. Confirm that Itoguruma is running, then run 'hataori itoguruma test'.",
                exception);
        }
    }

    private static async Task<object> ExecuteMcpAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2 || !args[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Usage: hataori mcp status [options]");
        }

        var options = ParseOptions(args.Skip(2));
        var configuration = LoadConfiguration(options);
        var server = configuration.GetRequiredSection(ServerOptions.SectionName).Get<ServerOptions>()
            ?? throw new InvalidOperationException("Server configuration is missing.");
        return await new McpEndpointProbe(NullLoggerFactory.Instance).ProbeAsync(server, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> ExecuteDoctorAsync(string[] args, CancellationToken cancellationToken)
    {
        var options = ParseOptions(args.Skip(1));
        var configuration = LoadConfiguration(options);
        var serverOptions = configuration.GetRequiredSection(ServerOptions.SectionName).Get<ServerOptions>()
            ?? throw new InvalidOperationException("Server configuration is missing.");
        var checks = new List<DoctorCheck>();
        await AddDoctorCheckAsync(checks, "configuration", () =>
        {
            var errors = CliConfigurationManager.ValidateConfiguration(configuration);
            return errors.Count == 0 ? Task.CompletedTask : throw new InvalidOperationException(string.Join(" ", errors));
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "server", async () =>
        {
            await new ControlPipeClient().SendAsync(serverOptions.ControlPipeName, "status", GetControlTimeout(options), cancellationToken).ConfigureAwait(false);
        }, skipPredicate: static exception => exception is UnauthorizedAccessException).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "itoguruma", async () =>
        {
            await ExecuteItogurumaAsync(["itoguruma", "status", "--config", GetConfigurationPath(options)], cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "mcp", async () =>
        {
            await ExecuteMcpAsync(["mcp", "status", "--config", GetConfigurationPath(options)], cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "sqlite", async () =>
        {
            var databasePath = ServerPaths.ResolveDatabasePath(serverOptions.DatabasePath, InstallationLayout.Resolve(AppContext.BaseDirectory).RootPath);
            await CliDatabaseDiagnostics.ExecuteAsync("integrity", databasePath, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "codex_cli", async () =>
        {
            var driver = configuration.GetRequiredSection(CodexDriverOptions.SectionName).Get<CodexDriverOptions>()
                ?? throw new InvalidOperationException("Codex configuration is missing.");
            await CheckExecutableAsync(driver.ExecutablePath, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "claude_cli", async () =>
        {
            var driver = configuration.GetRequiredSection(ClaudeCodeDriverOptions.SectionName).Get<ClaudeCodeDriverOptions>()
                ?? throw new InvalidOperationException("Claude Code configuration is missing.");
            await CheckExecutableAsync(driver.ExecutablePath, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "windows_service", async () =>
        {
            await new WindowsServiceManager(new SystemProcessRunner()).ExecuteAsync("status", "Hataori", null, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await AddDoctorCheckAsync(checks, "hooks", async () =>
        {
            var hookOptions = configuration.GetRequiredSection(HookOptions.SectionName).Get<HookOptions>()
                ?? throw new InvalidOperationException("Hook configuration is missing.");
            if (!hookOptions.Enabled)
            {
                throw new InvalidOperationException("Hooks are disabled.");
            }

            var baseDirectory = InstallationLayout.Resolve(AppContext.BaseDirectory).RootPath;
            await HookDiagnostics.CheckAsync([hookOptions.CodexConfigPath, hookOptions.ClaudeConfigPath], baseDirectory, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return new { healthy = checks.All(check => check.Ok || check.Skipped), checks };
    }

    private static async Task CheckExecutableAsync(string executablePath, CancellationToken cancellationToken)
    {
        var result = await new SystemProcessRunner().RunAsync(executablePath, ["--version"], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{executablePath} --version' failed with exit code {result.ExitCode}.");
        }
    }

    private static async Task AddDoctorCheckAsync(List<DoctorCheck> checks, string name, Func<Task> action, Func<Exception, bool>? skipPredicate = null)
    {
        try
        {
            await action().ConfigureAwait(false);
            checks.Add(new DoctorCheck(name, true, null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (skipPredicate is not null && skipPredicate(exception))
            {
                checks.Add(new DoctorCheck(name, false, $"Skipped: this check requires the same account as the Hataori Service (e.g. SYSTEM). {exception.Message}", Skipped: true));
                return;
            }

            checks.Add(new DoctorCheck(name, false, exception.Message));
        }
    }

    private static IConfigurationRoot LoadConfiguration(IReadOnlyDictionary<string, string> options) =>
        CliConfigurationManager.LoadConfiguration(GetConfigurationPath(options));

    private static string GetConfigurationPath(IReadOnlyDictionary<string, string> options) =>
        Path.GetFullPath(Optional(options, "config") ?? Environment.GetEnvironmentVariable("HATAORI_CONFIG_PATH") ?? InstallationLayout.Resolve(AppContext.BaseDirectory).ConfigurationPath);

    private static async Task<object> ExecuteConfigAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Usage: hataori config <init|show|path|check|reload> [options]");
        }

        var options = ParseOptions(args.Skip(2));
        if (args[1].Equals("init", StringComparison.OrdinalIgnoreCase))
        {
            var layout = InstallationLayout.Resolve(AppContext.BaseDirectory);
            layout.EnsureDirectories();
            var configurationPath = GetConfigurationPath(options);
            var created = await DefaultConfigurationWriter.EnsureAsync(configurationPath, cancellationToken).ConfigureAwait(false);
            return new { path = configurationPath, created };
        }

        if (args[1].Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            return await new ControlPipeClient().SendAsync(GetPipeName(options), "reload", GetControlTimeout(options), cancellationToken).ConfigureAwait(false);
        }

        var path = GetConfigurationPath(options);
        return await new CliConfigurationManager().ExecuteAsync(args[1], path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> ExecuteServiceAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException("Usage: hataori service <setup|install|uninstall|start|stop|restart|status> [options]");
        }

        if (args[1].Equals("setup", StringComparison.OrdinalIgnoreCase))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Windows Service setup is available only on Windows.");
            }

            return await new WindowsServiceSetupService(new SystemEnvironmentVariableStore(), new WindowsServiceCredentialStore())
                .ConfigureAsync(cancellationToken).ConfigureAwait(false);
        }

        var options = ParseOptions(args.Skip(2));
        var serviceName = Optional(options, "name") ?? "Hataori";
        var manager = new WindowsServiceManager(new SystemProcessRunner());
        return await manager.ExecuteAsync(args[1], serviceName, Optional(options, "server"), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsDatabaseCommand(string command) =>
        command.Equals("agent", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("queue", StringComparison.OrdinalIgnoreCase) ||
        command.Equals("db", StringComparison.OrdinalIgnoreCase);

    private static async Task<object> ExecuteDatabaseCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length < 2)
        {
            throw new ArgumentException($"Usage: hataori {args[0]} <command> [options]");
        }

        var hasPositional = args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal);
        var positional = hasPositional ? args[2] : null;
        var options = ParseOptions(args.Skip(hasPositional ? 3 : 2));
        var databasePath = GetDatabasePath(options);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();
        return args[0].ToLowerInvariant() switch
        {
            "agent" => await ExecuteAgentAsync(args[1], positional, options, connectionString, cancellationToken).ConfigureAwait(false),
            "conversation" => await ExecuteConversationAsync(args[1], positional, options, connectionString, cancellationToken).ConfigureAwait(false),
            "queue" => await ExecuteQueueAsync(args[1], positional, options, connectionString, cancellationToken).ConfigureAwait(false),
            "db" => await CliDatabaseDiagnostics.ExecuteAsync(args[1], databasePath, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown command '{args[0]}'."),
        };
    }

    private static async Task<object> ExecuteAgentAsync(string command, string? positional, IReadOnlyDictionary<string, string> options, string connectionString, CancellationToken cancellationToken)
    {
        var repository = new SqliteAgentRunRepository(connectionString);
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (command.Equals("runs", StringComparison.OrdinalIgnoreCase))
        {
            return await repository.ListAsync(ParseRunStatus(Optional(options, "status")), Optional(options, "agent"), cancellationToken).ConfigureAwait(false);
        }

        if (!command.Equals("list", StringComparison.OrdinalIgnoreCase) && !command.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Unknown agent command '{command}'.");
        }

        var configuration = LoadConfiguration(options);
        var activation = configuration.GetRequiredSection(ActivationOptions.SectionName).Get<ActivationOptions>() ?? new ActivationOptions();
        var running = await repository.ListAsync(AgentRunStatus.Running, null, cancellationToken).ConfigureAwait(false);
        var summaries = new[]
        {
            CreateAgentSummary("codex", configuration.GetSection(CodexDriverOptions.SectionName).Exists(), activation, running),
            CreateAgentSummary("claude-code", configuration.GetSection(ClaudeCodeDriverOptions.SectionName).Exists(), activation, running),
        };
        if (command.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var agentId = positional ?? Required(options, "agent");
            return summaries.FirstOrDefault(item => item.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        }

        return summaries;
    }

    private static AgentSummary CreateAgentSummary(string agentId, bool configured, ActivationOptions activation, IReadOnlyList<AgentRun> running) =>
        new(agentId, configured, running.Count(run => run.AgentId.Equals(agentId, StringComparison.OrdinalIgnoreCase)), activation.MaxConcurrentRuns.GetValueOrDefault(agentId));

    private static async Task<object> ExecuteConversationAsync(string command, string? positional, IReadOnlyDictionary<string, string> options, string connectionString, CancellationToken cancellationToken)
    {
        var repository = new SqliteConversationSessionRepository(connectionString);
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (command.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return await repository.ListAsync(ParseSessionStatus(Optional(options, "status")), Optional(options, "agent"), cancellationToken).ConfigureAwait(false);
        }

        var conversationId = positional ?? Required(options, "conversation");
        var agentId = Required(options, "agent");
        var service = new ConversationSessionService(repository, TimeProvider.System);
        if (command.Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            return await service.GetAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Conversation '{conversationId}/{agentId}' was not found.");
        }

        if (command.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            return await service.InvalidateAsync(conversationId, agentId, cancellationToken).ConfigureAwait(false);
        }

        throw new ArgumentException($"Unknown conversation command '{command}'.");
    }

    private static async Task<object> ExecuteQueueAsync(string command, string? positional, IReadOnlyDictionary<string, string> options, string connectionString, CancellationToken cancellationToken)
    {
        var repository = new SqliteMessageQueueRepository(connectionString);
        await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (command.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            return await repository.ListAsync(Optional(options, "agent"), cancellationToken).ConfigureAwait(false);
        }

        var messageId = positional ?? Required(options, "message");
        if (command.Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            return await repository.GetQueuedAsync(messageId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Queued message '{messageId}' was not found.");
        }

        if (command.Equals("retry", StringComparison.OrdinalIgnoreCase))
        {
            return await repository.RetryAsync(messageId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        }

        if (command.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            await repository.CancelQueuedAsync(messageId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return new { message_id = messageId, status = "cancelled" };
        }

        throw new ArgumentException($"Unknown queue command '{command}'.");
    }

    private static async Task<object> ExecuteServerAsync(string command, IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var client = new ControlPipeClient();
        return command.ToLowerInvariant() switch
        {
            "start" => new ServerProcessLauncher().Start(GetServerPath(options)),
            "stop" => await client.SendAsync(GetPipeName(options), "stop", GetControlTimeout(options), cancellationToken).ConfigureAwait(false),
            "status" => await client.SendAsync(GetPipeName(options), "status", GetControlTimeout(options), cancellationToken).ConfigureAwait(false),
            "restart" => await RestartAsync(client, options, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown command '{command}'."),
        };
    }

    private static async Task<ServerProcessResult> RestartAsync(ControlPipeClient client, IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var pipeName = GetPipeName(options);
        var timeout = GetControlTimeout(options);
        await client.SendAsync(pipeName, "stop", timeout, cancellationToken).ConfigureAwait(false);
        var deadline = TimeProvider.System.GetUtcNow() + timeout;
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            try
            {
                await client.SendAsync(pipeName, "status", TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return new ServerProcessLauncher().Start(GetServerPath(options));
            }
            catch (IOException)
            {
                return new ServerProcessLauncher().Start(GetServerPath(options));
            }
        }

        throw new InvalidOperationException("Hataori Server did not stop before the restart timeout.");
    }

    private static async Task<object?> ExecuteTaskAsync(string command, string? positional, IReadOnlyDictionary<string, string> options, TaskService service, CancellationToken cancellationToken)
    {
        var taskId = positional ?? Optional(options, "id");
        return command.ToLowerInvariant() switch
        {
            "start" => await service.StartAsync(Required(options, "id"), Required(options, "name"), Required(options, "agent"), Optional(options, "conversation"), Optional(options, "message"), Optional(options, "summary") ?? string.Empty, Optional(options, "current-work") ?? string.Empty, cancellationToken).ConfigureAwait(false),
            "get" => await GetTaskDetailsAsync(RequiredTaskId(taskId), service, cancellationToken).ConfigureAwait(false),
            "list" => await ListTasksAsync(options, service, cancellationToken).ConfigureAwait(false),
            "heartbeat" => await service.HeartbeatAsync(RequiredTaskId(taskId), Required(options, "current-work"), ParseProgress(Required(options, "progress")), Optional(options, "message"), cancellationToken).ConfigureAwait(false),
            "complete" => await service.CompleteAsync(RequiredTaskId(taskId), Optional(options, "message") ?? Required(options, "result"), cancellationToken).ConfigureAwait(false),
            "cancel" => await service.CancelAsync(RequiredTaskId(taskId), Optional(options, "message") ?? Optional(options, "result"), cancellationToken).ConfigureAwait(false),
            "fail" => await service.FailAsync(RequiredTaskId(taskId), Required(options, "result"), cancellationToken).ConfigureAwait(false),
            "expire" => await service.ExpireAsync(RequiredTaskId(taskId), cancellationToken).ConfigureAwait(false),
            "history" => await service.GetHistoryAsync(RequiredTaskId(taskId), cancellationToken).ConfigureAwait(false),
            "relation-add" => await AddRelationAsync(service, options, cancellationToken).ConfigureAwait(false),
            "relations" => await service.GetRelationsAsync(Required(options, "id"), cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown task command '{command}'."),
        };
    }

    private static async Task<object> GetTaskDetailsAsync(string taskId, TaskService service, CancellationToken cancellationToken)
    {
        var task = await service.GetAsync(taskId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        var history = await service.GetHistoryAsync(taskId, cancellationToken).ConfigureAwait(false);
        var relations = await service.GetRelationsAsync(taskId, cancellationToken).ConfigureAwait(false);
        return new { task, history, relations };
    }

    private static async Task<object> ListTasksAsync(IReadOnlyDictionary<string, string> options, TaskService service, CancellationToken cancellationToken)
    {
        HataoriTaskStatus? status = options.ContainsKey("all") ? null : ParseStatus(Optional(options, "status")) ?? HataoriTaskStatus.Active;
        var tasks = await service.ListAsync(status, Optional(options, "agent"), cancellationToken).ConfigureAwait(false);
        var conversationId = Optional(options, "conversation");
        return conversationId is null ? tasks : tasks.Where(task => task.ConversationId == conversationId).ToArray();
    }

    private static async Task<object> AddRelationAsync(TaskService service, IReadOnlyDictionary<string, string> options, CancellationToken cancellationToken)
    {
        var relation = new TaskRelation(Required(options, "id"), Required(options, "related-id"), Required(options, "type"));
        await service.AddRelationAsync(relation.TaskId, relation.RelatedTaskId, relation.RelationType, cancellationToken).ConfigureAwait(false);
        return relation;
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> arguments)
    {
        var values = arguments.ToArray();
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < values.Length; index++)
        {
            if (!values[index].StartsWith("--", StringComparison.Ordinal) || values[index].Length == 2)
            {
                throw new ArgumentException($"Invalid option '{values[index]}'.");
            }

            var name = values[index][2..];
            if (IsFlag(name))
            {
                options[name] = "true";
                continue;
            }

            if (++index >= values.Length || values[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option '--{name}' must have a value.");
            }

            options[name] = values[index];
        }

        return options;
    }

    private static string GetDatabasePath(IReadOnlyDictionary<string, string> options)
    {
        var path = Optional(options, "database") ?? Environment.GetEnvironmentVariable("HATAORI_DATABASE_PATH");
        return string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Specify --database or HATAORI_DATABASE_PATH.") : Path.GetFullPath(path);
    }

    private static string GetPipeName(IReadOnlyDictionary<string, string> options)
    {
        var pipeName = Optional(options, "pipe") ?? Environment.GetEnvironmentVariable("HATAORI_CONTROL_PIPE_NAME");
        return string.IsNullOrWhiteSpace(pipeName) ? throw new ArgumentException("Specify --pipe or HATAORI_CONTROL_PIPE_NAME.") : pipeName;
    }

    private static string GetServerPath(IReadOnlyDictionary<string, string> options)
    {
        var path = Optional(options, "server") ?? Environment.GetEnvironmentVariable("HATAORI_SERVER_PATH");
        return string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("Specify --server or HATAORI_SERVER_PATH.") : path;
    }

    private static TimeSpan GetControlTimeout(IReadOnlyDictionary<string, string> options)
    {
        var value = Optional(options, "timeout-seconds") ?? Environment.GetEnvironmentVariable("HATAORI_CONTROL_TIMEOUT_SECONDS") ?? "10";
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds is < 1 or > 300)
        {
            throw new ArgumentException("Specify --timeout-seconds between 1 and 300 or HATAORI_CONTROL_TIMEOUT_SECONDS.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name)
    {
        var value = Optional(options, name);
        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"Missing --{name}.") : value;
    }

    private static string RequiredTaskId(string? taskId) => string.IsNullOrWhiteSpace(taskId) ? throw new ArgumentException("Specify a task ID as an argument or with --id.") : taskId;
    private static bool IsFlag(string name) => name.Equals("json", StringComparison.OrdinalIgnoreCase) || name.Equals("all", StringComparison.OrdinalIgnoreCase) || name.Equals("follow", StringComparison.OrdinalIgnoreCase) || name.Equals("skip-test", StringComparison.OrdinalIgnoreCase);

    private static string? Optional(IReadOnlyDictionary<string, string> options, string name) => options.TryGetValue(name, out var value) ? value : null;
    private static int ParseProgress(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var progress) ? progress : throw new ArgumentException("--progress must be an integer.");
    private static int ParseLineCount(string? value) => value is null ? 200 : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count is >= 1 and <= 100000 ? count : throw new ArgumentException("--lines must be between 1 and 100000.");
    private static HataoriTaskStatus? ParseStatus(string? value) => value is null ? null : Enum.TryParse<HataoriTaskStatus>(value, true, out var status) ? status : throw new ArgumentException($"Invalid task status '{value}'.");
    private static AgentRunStatus? ParseRunStatus(string? value) => value is null ? null : Enum.TryParse<AgentRunStatus>(value, true, out var status) ? status : throw new ArgumentException($"Invalid agent run status '{value}'.");
    private static ConversationSessionStatus? ParseSessionStatus(string? value) => value is null ? null : Enum.TryParse<ConversationSessionStatus>(value, true, out var status) ? status : throw new ArgumentException($"Invalid conversation status '{value}'.");
    private static bool IsHelpCommand(IReadOnlyList<string> args) => args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase);
    private static bool IsVersionCommand(IReadOnlyList<string> args) => args[0].Equals("version", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--version", StringComparison.OrdinalIgnoreCase);
    private static bool IsSubcommandHelp(IReadOnlyList<string> args) => args.Count > 1 && (args[1].Equals("help", StringComparison.OrdinalIgnoreCase) || args[1].Equals("--help", StringComparison.OrdinalIgnoreCase));
    private static string GetVersion() => typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "unknown";
    private static string GetHelpText() => "Usage: hataori <start|stop|restart|status|service|task|agent|conversation|queue|db|config|setup|itoguruma|mcp|doctor|logs|monitor|hook|version|help> [command] [options]";
    private static string GetSubcommandHelp(string command) => $"Usage: hataori {command} <command> [arguments] [options]";

    private sealed record DoctorCheck(string Name, bool Ok, string? Error, bool Skipped = false);
    private sealed record AgentSummary(string AgentId, bool Enabled, int Running, int MaxRuns);
}
