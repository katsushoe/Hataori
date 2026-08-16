using System.Globalization;
using Hataori.Application;
using Microsoft.Extensions.Logging;

namespace Hataori.Monitor;

/// <summary>Monitorの例外を標準ログディレクトリへ保存します。</summary>
public sealed class MonitorFileLogger : ILogger
{
    private readonly object _sync = new();

    /// <summary>現在のMonitorログファイルパスを取得します。</summary>
    public string LogPath { get; } = Path.Combine(
        InstallationLayout.Resolve(AppContext.BaseDirectory).LogsPath,
        $"hataori-monitor-{DateTimeOffset.Now:yyyyMMdd}.log");

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var level = logLevel >= LogLevel.Error ? "[E]" : logLevel == LogLevel.Warning ? "[W]" : "[I]";
        var exceptionText = exception is null ? string.Empty : $" exception={Sanitize(exception.ToString())}";
        var line = $"{DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)} {level} [Monitor] {Sanitize(formatter(state, exception))}{exceptionText} Monitor.cs（0）{Environment.NewLine}";
        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line);
        }
    }

    private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
