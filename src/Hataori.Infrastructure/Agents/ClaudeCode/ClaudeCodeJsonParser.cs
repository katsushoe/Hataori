using System.Text.Json;

namespace Hataori.Infrastructure.Agents.ClaudeCode;

public sealed record ClaudeCodeJsonResult(string? NativeSessionId, string? FinalMessage, string? Error);

public static class ClaudeCodeJsonParser
{
    public static ClaudeCodeJsonResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonException? lastException = null;
        foreach (var candidate in GetCandidates(json))
        {
            try
            {
                using var document = JsonDocument.Parse(candidate);
                return ParseRoot(document.RootElement);
            }
            catch (JsonException exception)
            {
                lastException = exception;
            }
        }

        var lineCount = json.Count(character => character == '\n') + 1;
        var location = lastException is null
            ? "unknown"
            : $"line={lastException.LineNumber}, byte={lastException.BytePositionInLine}";
        throw new FormatException(
            $"Claude Code output did not contain a valid JSON result (characters={json.Length}, lines={lineCount}, {location}).",
            lastException);
    }

    private static ClaudeCodeJsonResult ParseRoot(JsonElement root)
    {
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

    private static IEnumerable<string> GetCandidates(string output)
    {
        var normalized = output.Trim().TrimStart('\uFEFF');
        if (normalized.Length > 0)
        {
            yield return normalized;
        }

        foreach (var line in normalized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
        {
            var candidate = line.TrimStart('\uFEFF');
            if (candidate.Length > 0 && !string.Equals(candidate, normalized, StringComparison.Ordinal))
            {
                yield return candidate;
            }
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
