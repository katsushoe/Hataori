namespace Hataori.Server;

/// <summary>Itoguruma MCPへの現在の接続状態をMonitorへ公開します。</summary>
public sealed class ItogurumaConnectionState
{
    private string _value = "connecting";

    /// <summary>現在の接続状態を取得します。</summary>
    public string Value => Volatile.Read(ref _value);

    /// <summary>接続状態を更新します。</summary>
    public void Set(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Volatile.Write(ref _value, value);
    }
}
