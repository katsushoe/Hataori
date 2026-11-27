using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

/// <summary>テスト専用のMonitorログ出力先です。実際のユーザーログ（%LOCALAPPDATA%\Hataori\Monitor\logs）へは書き込みません。</summary>
internal static class TestMonitorLogs
{
    public static readonly string Directory = Path.Combine(Path.GetTempPath(), "hataori-monitor-tests", "logs");

    public static MonitorFileLogger CreateLogger() => new(Directory);

    public static MonitorErrorHandler CreateErrorHandler() => new(CreateLogger());
}
