using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Hataori.Server;

/// <summary>
/// 構造化情報を保持した日次ファイルログProviderです。
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directoryPath;
    private readonly LogLevel _minimumLevel;
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writerTask;

    public FileLoggerProvider(FileLogOptions options, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _directoryPath = Path.IsPathFullyQualified(options.DirectoryPath)
            ? Path.GetFullPath(options.DirectoryPath)
            : Path.GetFullPath(Path.Combine(baseDirectory, options.DirectoryPath));
        _minimumLevel = Enum.Parse<LogLevel>(options.MinimumLevel, true);
        Directory.CreateDirectory(_directoryPath);
        DeleteExpiredLogs(options.RetentionDays);
        _writerTask = Task.Run(WriteLoopAsync);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _minimumLevel, _lines.Writer);

    /// <inheritdoc />
    public void Dispose()
    {
        _lines.Writer.TryComplete();
        _writerTask.GetAwaiter().GetResult();
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var line in _lines.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var path = Path.Combine(_directoryPath, $"hataori-{DateTimeOffset.Now:yyyyMMdd}.log");
            await File.AppendAllTextAsync(path, line + Environment.NewLine).ConfigureAwait(false);
        }
    }

    private void DeleteExpiredLogs(int retentionDays)
    {
        var threshold = DateTimeOffset.Now.AddDays(-retentionDays);
        foreach (var path in Directory.EnumerateFiles(_directoryPath, "hataori-*.log", SearchOption.TopDirectoryOnly))
        {
            if (File.GetLastWriteTimeUtc(path) < threshold.UtcDateTime)
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FileLogger(string categoryName, LogLevel minimumLevel, ChannelWriter<string> writer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var values = state is IEnumerable<KeyValuePair<string, object?>> structured
                ? structured.Where(pair => pair.Key != "{OriginalFormat}").ToArray()
                : [];
            var properties = string.Join(' ', values.Select(FormatProperty));
            var suffix = string.IsNullOrEmpty(properties) ? string.Empty : $" {properties}";
            var exceptionText = exception is null ? string.Empty : $" exception={Sanitize(exception.ToString())}";
            var category = categoryName.Split('.').LastOrDefault() ?? categoryName;
            var message = Redact(formatter(state, exception), values);
            var line = $"{DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)} {Level(logLevel)} [{category}] {Sanitize(message)}{suffix}{exceptionText} {category}.cs（0）";
            writer.TryWrite(line);
        }

        private static string FormatProperty(KeyValuePair<string, object?> pair) =>
            $"{pair.Key}={(IsSecret(pair.Key) ? "(redacted)" : Sanitize(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty))}";

        private static bool IsSecret(string key) => key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Contains("credential", StringComparison.OrdinalIgnoreCase);
        private static string Redact(string message, IEnumerable<KeyValuePair<string, object?>> values)
        {
            foreach (var pair in values.Where(pair => IsSecret(pair.Key) && pair.Value is not null))
            {
                var secret = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(secret))
                {
                    message = message.Replace(secret, "(redacted)", StringComparison.Ordinal);
                }
            }

            return message;
        }

        private static string Sanitize(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
        private static string Level(LogLevel level) => level switch { LogLevel.Trace or LogLevel.Debug => "[D]", LogLevel.Information => "[I]", LogLevel.Warning => "[W]", LogLevel.Error or LogLevel.Critical => "[E]", _ => "[I]" };
    }
}
