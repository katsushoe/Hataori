namespace Hataori.Core.Workspaces;

/// <summary>Workspaceを一意に識別する正規化済みIDです。</summary>
public static class WorkspaceId
{
    /// <summary>既存データとWorkspace未指定の呼び出しに割り当てるIDです。</summary>
    public const string Default = "default";

    /// <summary>Workspace IDを検証してInvariant lowercaseへ正規化します。</summary>
    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? Default : value.Trim().ToLowerInvariant();
        if (!char.IsAsciiLetter(normalized[0]) || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("Workspace ID must match ^[a-z][a-z0-9]*$.", nameof(value));
        }

        return normalized;
    }
}
