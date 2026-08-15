namespace Hataori.Cli;

/// <summary>
/// Windows Service Control Managerを介してHataori Serverを管理します。
/// </summary>
public sealed class WindowsServiceManager(IProcessRunner processRunner)
{
    /// <summary>
    /// Windows Service管理コマンドを実行します。
    /// </summary>
    public async Task<object> ExecuteAsync(string command, string serviceName, string? serverPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Service commands are available only on Windows.");
        }

        var normalized = command.ToLowerInvariant();
        if (normalized == "restart")
        {
            await RunAsync(serviceName, ["stop", serviceName], cancellationToken).ConfigureAwait(false);
            return await RunAsync(serviceName, ["start", serviceName], cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<string> arguments = normalized switch
        {
            "install" => BuildInstallArguments(serviceName, serverPath),
            "uninstall" => ["delete", serviceName],
            "start" => ["start", serviceName],
            "stop" => ["stop", serviceName],
            "status" => ["query", serviceName],
            _ => throw new ArgumentException($"Unknown service command '{command}'."),
        };
        return await RunAsync(serviceName, arguments, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> BuildInstallArguments(string serviceName, string? serverPath)
    {
        if (string.IsNullOrWhiteSpace(serverPath))
        {
            throw new ArgumentException("Specify --server for service installation.");
        }

        var fullPath = Path.GetFullPath(serverPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Hataori Server executable was not found.", fullPath);
        }

        return ["create", serviceName, "binPath=", fullPath, "start=", "auto", "DisplayName=", "Hataori Server"];
    }

    private async Task<object> RunAsync(string serviceName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync("sc.exe", arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            throw new InvalidOperationException($"Service command failed with exit code {result.ExitCode}: {detail.Trim()}");
        }

        return new { service_name = serviceName, command = arguments[0], success = true, output = result.StandardOutput.Trim() };
    }
}
