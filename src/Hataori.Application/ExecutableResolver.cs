namespace Hataori.Application;

/// <summary>
/// PATH上の拡張子なしCommand名を、Windowsが実行可能な拡張子（PATHEXT）付きの実際のfileへ解決します。
/// .NETの<see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/>は
/// UseShellExecute=falseの場合、拡張子なし名から`.cmd`／`.ps1`等のシムを自動解決しないため、
/// npmなどが生成するCLIツールを起動する前にこの解決を行う。
/// </summary>
public static class ExecutableResolver
{
    private static readonly string[] DefaultExtensions = [".com", ".exe", ".bat", ".cmd"];

    /// <summary>
    /// <paramref name="command"/>を実行可能なfull pathへ解決します。既に絶対pathまたは実在するfileの場合、
    /// あるいは解決先が見つからない場合は元の値をそのまま返します（既存の挙動・error messageを維持する）。
    /// </summary>
    public static string Resolve(string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            return command;
        }

        var extensions = ParsePathExtensions();
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Resolve(command, directories, extensions);
    }

    /// <summary>
    /// テスト用に検索対象ディレクトリと拡張子を明示指定できるcore実装です。実運用のPATH／PATHEXTには依存しません。
    /// </summary>
    public static string Resolve(string command, IReadOnlyList<string> searchDirectories, IReadOnlyList<string> extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(searchDirectories);
        ArgumentNullException.ThrowIfNull(extensions);
        if (Path.IsPathRooted(command) || File.Exists(command))
        {
            return command;
        }

        if (Path.GetFileName(command) != command)
        {
            // ディレクトリ区切りを含む相対pathはPATH探索の対象外とする。
            return command;
        }

        var hasExtension = !string.IsNullOrEmpty(Path.GetExtension(command));

        foreach (var directory in searchDirectories)
        {
            if (hasExtension)
            {
                var direct = SafeCombine(directory, command);
                if (direct is not null && File.Exists(direct))
                {
                    return direct;
                }

                continue;
            }

            foreach (var extension in extensions)
            {
                var candidate = SafeCombine(directory, command + extension);
                if (candidate is not null && File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    private static string? SafeCombine(string directory, string fileName)
    {
        try
        {
            return Path.Combine(directory, fileName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string[] ParsePathExtensions()
    {
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExt))
        {
            return DefaultExtensions;
        }

        return pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
