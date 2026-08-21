using System.Text.Json;

namespace Hataori.Cli;

/// <summary>Hook設定ファイルの存在と必須イベントを診断します。</summary>
public static class HookDiagnostics
{
    private static readonly string[] RequiredEvents = ["SessionStart", "UserPromptSubmit", "PreToolUse", "Stop"];

    public static async Task CheckAsync(IEnumerable<string> paths, string baseDirectory, CancellationToken cancellationToken)
    {
        foreach (var configuredPath in paths)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new InvalidOperationException(Hataori.Application.Localization.DisplayLanguage.Text("Hook設定パスがありません。", "Hook configuration path is missing."));
            }

            var path = Path.IsPathFullyQualified(configuredPath) ? configuredPath : Path.Combine(baseDirectory, configuredPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(Hataori.Application.Localization.DisplayLanguage.Text("Hook設定ファイルが見つかりません。", "Hook configuration file was not found."), path);
            }

            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var hooks = root.TryGetProperty("hooks", out var direct) ? direct : root.GetProperty("hooks");
            foreach (var eventName in RequiredEvents)
            {
                if (!hooks.TryGetProperty(eventName, out var handlers) || handlers.ValueKind != JsonValueKind.Array || handlers.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException(Hataori.Application.Localization.DisplayLanguage.Text($"Hook設定'{path}'に{eventName}がありません。", $"Hook configuration '{path}' is missing {eventName}."));
                }
            }
        }
    }
}
