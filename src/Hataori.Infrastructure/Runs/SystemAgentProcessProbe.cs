using System.Diagnostics;
using System.ComponentModel;
using Hataori.Application.Runs;

namespace Hataori.Infrastructure.Runs;

/// <summary>PIDと開始日時を照合してAgent Processの生存を確認します。</summary>
public sealed class SystemAgentProcessProbe : IAgentProcessProbe
{
    private static readonly TimeSpan StartTimeTolerance = TimeSpan.FromSeconds(30);

    public bool IsRunning(int processId, DateTimeOffset? expectedStartedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return false;
            }

            return expectedStartedAtUtc is null || (new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero) - expectedStartedAtUtc.Value).Duration() <= StartTimeTolerance;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
