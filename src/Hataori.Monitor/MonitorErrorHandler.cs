using Microsoft.Extensions.Logging;

namespace Hataori.Monitor;

/// <summary>Monitorの例外を記録し、利用者向けの対処方法を表示します。</summary>
public sealed class MonitorErrorHandler(MonitorFileLogger logger)
{
    /// <summary>例外をログへ保存し、必要なら案内ダイアログを表示します。</summary>
    public void Report(Exception exception, IWin32Window? owner, string guidance, bool showDialog)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(guidance);
        string logNotice;
        try
        {
            logger.LogError(exception, "[Error] Monitor operation failed");
            logNotice = $"詳細ログ: {logger.LogPath}";
        }
        catch (Exception logException)
        {
            logNotice = $"ログの保存にも失敗しました: {logException.Message}";
        }

        if (showDialog)
        {
            MessageBox.Show(owner, $"{guidance}{Environment.NewLine}{Environment.NewLine}{logNotice}", "Hataori Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
