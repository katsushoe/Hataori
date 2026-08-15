namespace Hataori.Cli;

/// <summary>
/// 外部プロセスを起動します。
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// 指定されたコマンドを実行し、完了結果を返します。
    /// </summary>
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>
/// 外部プロセスの完了結果です。
/// </summary>
public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);
