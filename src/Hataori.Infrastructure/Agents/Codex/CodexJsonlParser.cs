using System.Text.Json;

namespace Hataori.Infrastructure.Agents.Codex;

public sealed record CodexJsonlResult(string? NativeSessionId, string? FinalMessage, string? Error);

public static class CodexJsonlParser
{
    public static CodexJsonlResult Parse(string jsonl)
    {
        ArgumentNullException.ThrowIfNull(jsonl);
        string? nativeSessionId = null;
        string? finalMessage = null;
        string? error = null;
        var lineNumber = 0;
        using var reader = new StringReader(jsonl);
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = GetString(root, "type");
                if (type == "thread.started")
                {
                    nativeSessionId = GetString(root, "thread_id") ?? nativeSessionId;
                }
                else if (type == "item.completed" && root.TryGetProperty("item", out var item))
                {
                    var itemType = GetString(item, "type");
                    if (itemType == "agent_message")
                    {
                        finalMessage = GetString(item, "text") ?? finalMessage;
                    }
                    else if (itemType == "error")
                    {
                        error = GetString(item, "message") ?? error;
                    }
                }
                else if (type is "error" or "turn.failed")
                {
                    error = GetString(root, "message") ?? GetNestedError(root) ?? error;
                }
            }
            catch (JsonException exception)
            {
                throw new FormatException($"Codex JSONL line {lineNumber} is invalid.", exception);
            }
        }

        return new CodexJsonlResult(nativeSessionId, finalMessage, error);
    }

    private static string? GetNestedError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(error, "message");
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
