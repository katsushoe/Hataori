using System.Text.Json;

namespace Hataori.Infrastructure.Agents.ClaudeCode;

public sealed record ClaudeCodeJsonResult(string? NativeSessionId, string? FinalMessage, string? Error);

public static class ClaudeCodeJsonParser
{
    public static ClaudeCodeJsonResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Claude Code output must be a JSON object.");
            }

            var sessionId = GetString(root, "session_id");
            var result = GetString(root, "result");
            var subtype = GetString(root, "subtype");
            var isError = root.TryGetProperty("is_error", out var errorProperty) && errorProperty.ValueKind == JsonValueKind.True;
            var error = isError || subtype is not null and not "success"
                ? result ?? GetString(root, "error") ?? $"Claude Code returned subtype '{subtype}'."
                : null;
            return new ClaudeCodeJsonResult(sessionId, error is null ? result : null, error);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Claude Code output is not valid JSON.", exception);
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
