namespace Hataori.Application.Control;

/// <summary>
/// ローカルControl Pipeの入力です。
/// </summary>
public sealed record ControlRequest(string Command);

/// <summary>
/// ローカルControl Pipeの応答です。
/// </summary>
public sealed record ControlResponse(bool Success, string Status, DateTimeOffset TimestampUtc);
