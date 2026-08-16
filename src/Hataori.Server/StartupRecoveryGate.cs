namespace Hataori.Server;

/// <summary>Activation開始を起動復旧完了まで待機させます。</summary>
public sealed class StartupRecoveryGate
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Ready => _completion.Task;
    public void Complete() => _completion.TrySetResult();
    public void Fail(Exception exception) => _completion.TrySetException(exception);
}
