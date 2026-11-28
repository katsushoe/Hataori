namespace Hataori.Server;

/// <summary><c>GET /health</c> の応答です。Serverが要求を処理できる状態なら <c>status</c> は <c>ok</c> です。</summary>
public sealed record HealthResponse(string Status, string Name, string Version)
{
    public static HealthResponse Create() =>
        new("ok", "Hataori", typeof(HealthResponse).Assembly.GetName().Version?.ToString() ?? "unknown");
}
