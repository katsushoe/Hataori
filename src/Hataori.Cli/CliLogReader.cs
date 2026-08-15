namespace Hataori.Cli;

/// <summary>
/// Hataoriファイルログの参照と追従を提供します。
/// </summary>
public sealed class CliLogReader
{
    /// <summary>
    /// 条件に一致する最新ログを取得します。
    /// </summary>
    public async Task<IReadOnlyList<string>> ReadAsync(string directoryPath, int lineCount, string? agentId, string? runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineCount);
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Log directory '{directoryPath}' was not found.");
        }

        var lines = new List<string>();
        foreach (var path in Directory.EnumerateFiles(directoryPath, "hataori-*.log").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            lines.AddRange(await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false));
        }

        return lines.Where(line => Matches(line, agentId, runId)).TakeLast(lineCount).ToArray();
    }

    /// <summary>
    /// 現在の最新ログを表示後、新規ログをポーリングして出力します。
    /// </summary>
    public async Task FollowAsync(string directoryPath, int lineCount, string? agentId, string? runId, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var initial = await ReadAsync(directoryPath, lineCount, agentId, runId, cancellationToken).ConfigureAwait(false);
        foreach (var line in initial)
        {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }

        var emittedCount = (await ReadAsync(directoryPath, int.MaxValue, agentId, runId, cancellationToken).ConfigureAwait(false)).Count;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
            var lines = await ReadAsync(directoryPath, int.MaxValue, agentId, runId, cancellationToken).ConfigureAwait(false);
            if (lines.Count < emittedCount)
            {
                emittedCount = 0;
            }

            foreach (var line in lines.Skip(emittedCount))
            {
                await output.WriteLineAsync(line).ConfigureAwait(false);
            }

            emittedCount = lines.Count;
        }
    }

    private static bool Matches(string line, string? agentId, string? runId) =>
        (string.IsNullOrWhiteSpace(agentId) || line.Contains($"AgentId={agentId}", StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(runId) || line.Contains($"RunId={runId}", StringComparison.OrdinalIgnoreCase));
}
