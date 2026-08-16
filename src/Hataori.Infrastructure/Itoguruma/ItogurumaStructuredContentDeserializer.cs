using System.Text.Json;

namespace Hataori.Infrastructure.Itoguruma;

/// <summary>Itoguruma MCP構造化結果の互換形式を解析します。</summary>
internal static class ItogurumaStructuredContentDeserializer
{
    /// <summary>`data`ラッパー形式と旧直接形式を解析します。</summary>
    internal static T Deserialize<T>(JsonElement content, JsonSerializerOptions options) where T : class
    {
        var value = content.ValueKind == JsonValueKind.Object && content.TryGetProperty("data", out var data)
            ? data
            : content;
        return value.Deserialize<T>(options)
            ?? throw new InvalidOperationException("Itoguruma returned invalid structured content.");
    }
}
