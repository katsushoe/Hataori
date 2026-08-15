using System.Diagnostics;
using System.Text;
using Hataori.Application.Runs;

namespace Hataori.Infrastructure.Runs;

/// <summary>
/// Shellを介さずAgent子プロセスを起動し、出力と終了状態を取得します。
/// </summary>
public sealed class SystemAgentProcessManager(TimeProvider timeProvider) : IAgentProcessManager
{
    public Task<IAgentProcess> StartAsync(AgentProcessStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxCapturedCharacters);
        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory '{request.WorkingDirectory}' was not found.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in request.Environment)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var startedAtUtc = timeProvider.GetUtcNow();
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Process '{request.FileName}' could not be started.");
            }

            return Task.FromResult<IAgentProcess>(new SystemAgentProcess(process, startedAtUtc, request.MaxCapturedCharacters, timeProvider));
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private sealed class SystemAgentProcess : IAgentProcess
    {
        private readonly Process _process;
        private readonly DateTimeOffset _startedAtUtc;
        private readonly TimeProvider _timeProvider;
        private readonly Task<CapturedText> _standardOutput;
        private readonly Task<CapturedText> _standardError;
        private int _disposed;

        public SystemAgentProcess(Process process, DateTimeOffset startedAtUtc, int maximumCharacters, TimeProvider timeProvider)
        {
            _process = process;
            _startedAtUtc = startedAtUtc;
            _timeProvider = timeProvider;
            _standardOutput = ReadBoundedAsync(process.StandardOutput, maximumCharacters);
            _standardError = ReadBoundedAsync(process.StandardError, maximumCharacters);
        }

        public int ProcessId => _process.Id;

        public async Task<AgentProcessResult> WaitForExitAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await CancelAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(_standardOutput, _standardError).ConfigureAwait(false);
                throw;
            }

            var output = await _standardOutput.ConfigureAwait(false);
            var error = await _standardError.ConfigureAwait(false);
            return new AgentProcessResult(
                _process.Id, _process.ExitCode, output.Text, error.Text, output.Truncated, error.Truncated,
                _startedAtUtc, _timeProvider.GetUtcNow());
        }

        public async Task CancelAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }

            _process.Dispose();
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed != 0, this);

        private static async Task<CapturedText> ReadBoundedAsync(StreamReader reader, int maximumCharacters)
        {
            var buffer = new char[8192];
            var builder = new StringBuilder(Math.Min(maximumCharacters, buffer.Length));
            var truncated = false;
            int count;
            while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                var remaining = maximumCharacters - builder.Length;
                if (remaining > 0)
                {
                    builder.Append(buffer, 0, Math.Min(count, remaining));
                }

                truncated |= count > remaining;
            }

            return new CapturedText(builder.ToString(), truncated);
        }

        private sealed record CapturedText(string Text, bool Truncated);
    }
}
