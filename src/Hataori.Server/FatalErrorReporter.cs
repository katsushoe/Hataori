using System.Globalization;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>プロセス境界へ到達した致命的例外を永続化し、利用者向け案内を生成します。</summary>
public static class FatalErrorReporter
{
    /// <summary>致命的例外を緊急ログへ記録します。</summary>
    public static async Task<FatalErrorReport> WriteAsync(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Hataori",
            "logs");
        var logPath = Path.Combine(logDirectory, $"hataori-fatal-{DateTimeOffset.Now:yyyyMMdd}.log");
        var saved = false;
        try
        {
            Directory.CreateDirectory(logDirectory);
            var entry = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz} [E] [Fatal] {exception}{Environment.NewLine}");
            await File.AppendAllTextAsync(logPath, entry, cancellationToken).ConfigureAwait(false);
            saved = true;
        }
        catch (Exception loggingException) when (loggingException is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"致命的エラーのログ保存にも失敗しました: {loggingException.Message}");
        }
        catch (Exception loggingException)
        {
            Console.Error.WriteLine($"致命的エラーのログ保存中に予期しないエラーが発生しました: {loggingException.Message}");
        }

        return new FatalErrorReport(CreateUserMessage(exception), saved ? logPath : null);
    }

    private static string CreateUserMessage(Exception exception)
    {
        if (exception is OptionsValidationException)
        {
            return "Hataori Serverの設定が正しくありません。hataori.jsonとHATAORI_環境変数を確認してから再起動してください。";
        }

        if (exception is FileNotFoundException)
        {
            return "Hataori Serverに必要なファイルが見つかりません。配置内容とhataori.jsonを確認してから再起動してください。";
        }

        if (exception is UnauthorizedAccessException)
        {
            return "Hataori Serverが必要なファイルへアクセスできません。実行アカウントの権限と設定パスを確認してください。";
        }

        return "Hataori Serverを継続できないエラーが発生したため安全に停止しました。詳細ログを確認し、設定・データベース・空き容量を確認してから再起動してください。";
    }
}

/// <summary>致命的例外の利用者向け報告です。</summary>
public sealed record FatalErrorReport(string UserMessage, string? LogPath);
