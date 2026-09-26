using System.Text.Json;

namespace Hataori.Cli;

/// <summary>Claude Codeプラグインの有効化状態を読み取り専用で診断します。</summary>
internal static class ClaudePluginDiagnostics
{
    internal static async Task CheckAsync(string settingsPath, string pluginId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var fullPath = Path.GetFullPath(settingsPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Claude Code settings were not found at '{fullPath}'.", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("enabledPlugins", out var enabledPlugins) || enabledPlugins.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Claude Code settings do not contain an enabledPlugins object.");
        }

        if (!enabledPlugins.TryGetProperty(pluginId, out var enabled) || enabled.ValueKind is not JsonValueKind.True)
        {
            throw new InvalidOperationException($"Claude Code plugin '{pluginId}' is not enabled.");
        }
    }
}
