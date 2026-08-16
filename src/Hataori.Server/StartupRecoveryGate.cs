namespace Hataori.Server;

/// <summary>Activation開始を起動復旧完了まで待機させます。</summary>
public sealed class StartupRecoveryGate
{
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> Ready => _completion.Task;
    public void Complete() => _completion.TrySetResult(true);
    public void Fail() => _completion.TrySetResult(false);
}
