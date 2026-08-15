using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hataori.Application.Tasks;
using Hataori.Core.Tasks;
using Hataori.Infrastructure.Tasks;
using Microsoft.Data.Sqlite;

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
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("Usage: hataori <start|stop|restart|status|task> [options]");
            }

            object? result;
            if (string.Equals(args[0], "task", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException("Usage: hataori task <command> [options]");
                }

                var options = ParseOptions(args.Skip(2));
                var connectionString = new SqliteConnectionStringBuilder { DataSource = GetDatabasePath(options), ForeignKeys = true }.ToString();
                var repository = new SqliteTaskRepository(connectionString);
                await repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
                result = await ExecuteTaskAsync(args[1], options, new TaskService(repository, TimeProvider.System), cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
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

    private static async Task<object?> ExecuteTaskAsync(string command, IReadOnlyDictionary<string, string> options, TaskService service, CancellationToken cancellationToken)
    {
        return command.ToLowerInvariant() switch
        {
            "start" => await service.StartAsync(Required(options, "id"), Required(options, "name"), Required(options, "agent"), Optional(options, "conversation"), Optional(options, "message"), Optional(options, "summary") ?? string.Empty, Optional(options, "current-work") ?? string.Empty, cancellationToken).ConfigureAwait(false),
            "get" => await service.GetAsync(Required(options, "id"), cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException($"Task '{Required(options, "id")}' was not found."),
            "list" => await service.ListAsync(ParseStatus(Optional(options, "status")), Optional(options, "agent"), cancellationToken).ConfigureAwait(false),
            "heartbeat" => await service.HeartbeatAsync(Required(options, "id"), Required(options, "current-work"), ParseProgress(Required(options, "progress")), cancellationToken).ConfigureAwait(false),
            "complete" => await service.CompleteAsync(Required(options, "id"), Required(options, "result"), cancellationToken).ConfigureAwait(false),
            "cancel" => await service.CancelAsync(Required(options, "id"), Optional(options, "result"), cancellationToken).ConfigureAwait(false),
            "fail" => await service.FailAsync(Required(options, "id"), Required(options, "result"), cancellationToken).ConfigureAwait(false),
            "expire" => await service.ExpireAsync(Required(options, "id"), cancellationToken).ConfigureAwait(false),
            "history" => await service.GetHistoryAsync(Required(options, "id"), cancellationToken).ConfigureAwait(false),
            "relation-add" => await AddRelationAsync(service, options, cancellationToken).ConfigureAwait(false),
            "relations" => await service.GetRelationsAsync(Required(options, "id"), cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown task command '{command}'."),
        };
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
        if (values.Length % 2 != 0)
        {
            throw new ArgumentException("Every option must have a value.");
        }

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < values.Length; index += 2)
        {
            if (!values[index].StartsWith("--", StringComparison.Ordinal) || values[index].Length == 2)
            {
                throw new ArgumentException($"Invalid option '{values[index]}'.");
            }

            options[values[index][2..]] = values[index + 1];
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
        var value = Optional(options, "timeout-seconds") ?? Environment.GetEnvironmentVariable("HATAORI_CONTROL_TIMEOUT_SECONDS");
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

    private static string? Optional(IReadOnlyDictionary<string, string> options, string name) => options.TryGetValue(name, out var value) ? value : null;
    private static int ParseProgress(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var progress) ? progress : throw new ArgumentException("--progress must be an integer.");
    private static HataoriTaskStatus? ParseStatus(string? value) => value is null ? null : Enum.TryParse<HataoriTaskStatus>(value, true, out var status) ? status : throw new ArgumentException($"Invalid task status '{value}'.");
}
