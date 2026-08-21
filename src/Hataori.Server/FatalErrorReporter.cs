using System.Globalization;
using Hataori.Application;
using Hataori.Application.Localization;
using Microsoft.Extensions.Options;

namespace Hataori.Server;

/// <summary>プロセス境界へ到達した致命的例外を永続化し、利用者向け案内を生成します。</summary>
public static class FatalErrorReporter
{
    /// <summary>致命的例外を緊急ログへ記録します。</summary>
    public static async Task<FatalErrorReport> WriteAsync(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var logDirectory = InstallationLayout.Resolve(AppContext.BaseDirectory).LogsPath;
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
            Console.Error.WriteLine(DisplayLanguage.Text($"致命的エラーのログ保存にも失敗しました: {loggingException.Message}", $"Saving the fatal error log also failed: {loggingException.Message}"));
        }
        catch (Exception loggingException)
        {
            Console.Error.WriteLine(DisplayLanguage.Text($"致命的エラーのログ保存中に予期しないエラーが発生しました: {loggingException.Message}", $"An unexpected error occurred while saving the fatal error log: {loggingException.Message}"));
        }

        return new FatalErrorReport(CreateUserMessage(exception), saved ? logPath : null);
    }

    private static string CreateUserMessage(Exception exception)
    {
        if (exception is OptionsValidationException)
        {
            return DisplayLanguage.Text("Hataori Serverの設定が正しくありません。hataori.jsonとHATAORI_環境変数を確認してから再起動してください。", "Hataori Server configuration is invalid. Check hataori.json and HATAORI_ environment variables, then restart.");
        }

        if (exception is FileNotFoundException)
        {
            return DisplayLanguage.Text("Hataori Serverに必要なファイルが見つかりません。配置内容とhataori.jsonを確認してから再起動してください。", "A file required by Hataori Server was not found. Check the installation and hataori.json, then restart.");
        }

        if (exception is UnauthorizedAccessException)
        {
            return DisplayLanguage.Text("Hataori Serverが必要なファイルへアクセスできません。実行アカウントの権限と設定パスを確認してください。", "Hataori Server cannot access a required file. Check the service account permissions and configuration path.");
        }

        return DisplayLanguage.Text("Hataori Serverを継続できないエラーが発生したため安全に停止しました。詳細ログを確認し、設定・データベース・空き容量を確認してから再起動してください。", "Hataori Server stopped safely after a fatal error. Check the detailed log, configuration, database, and available disk space before restarting.");
    }
}

/// <summary>致命的例外の利用者向け報告です。</summary>
public sealed record FatalErrorReport(string UserMessage, string? LogPath);
