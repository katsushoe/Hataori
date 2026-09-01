namespace Hataori.Cli;

/// <summary>doctorの個別診断を実行し、成功・失敗・Account差異skipへ分類します。</summary>
internal static class DoctorCheckRunner
{
    internal static async Task<DoctorCheck> RunAsync(string name, Func<Task> action, Func<Exception, bool>? skipPredicate = null)
    {
        try
        {
            await action().ConfigureAwait(false);
            return new DoctorCheck(name, true, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return skipPredicate is not null && skipPredicate(exception)
                ? new DoctorCheck(name, false, $"Skipped: this check requires the same account as the Hataori Service (e.g. SYSTEM). {exception.Message}", Skipped: true)
                : new DoctorCheck(name, false, exception.Message);
        }
    }
}

/// <summary>doctorの個別診断結果です。</summary>
internal sealed record DoctorCheck(string Name, bool Ok, string? Error, bool Skipped = false);
