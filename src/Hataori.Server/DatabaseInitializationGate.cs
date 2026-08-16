namespace Hataori.Server;

/// <summary>全SQLiteリポジトリの初期化完了を起動処理へ通知します。</summary>
public sealed class DatabaseInitializationGate
{
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>初期化の成否を返す完了通知です。</summary>
    public Task<bool> Ready => _completion.Task;

    /// <summary>初期化成功を通知します。</summary>
    public void Complete() => _completion.TrySetResult(true);

    /// <summary>初期化失敗を通知します。</summary>
    public void Fail() => _completion.TrySetResult(false);
}
