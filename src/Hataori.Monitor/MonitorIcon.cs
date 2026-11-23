namespace Hataori.Monitor;

/// <summary>Monitorへ埋め込んだHataoriアイコンを読み込みます。</summary>
internal static class MonitorIcon
{
    internal const string ResourceName = "Hataori.Monitor.hataori.ico";

    /// <summary>指定サイズに最も近いフレームを選んでアイコンを作成します。</summary>
    internal static Icon Load(Size size)
    {
        using var stream = typeof(MonitorIcon).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded icon resource '{ResourceName}' was not found.");
        return new Icon(stream, size);
    }
}
