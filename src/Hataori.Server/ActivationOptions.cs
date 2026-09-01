namespace Hataori.Server;

public sealed class ActivationOptions
{
    public const string SectionName = "activation";

    /// <summary>providerPriorityが設定に存在しない場合に使う既定値です。</summary>
    public static readonly IReadOnlyList<string> DefaultProviderPriority = ["codex", "claude-code"];

    public bool Enabled { get; init; }
    public string WorkspaceId { get; init; } = Hataori.Core.Workspaces.WorkspaceId.Default;
    public string WorkingDirectory { get; init; } = string.Empty;
    public IReadOnlyList<ActivationWorkspaceOptions> Workspaces { get; init; } = [];
    public int PollIntervalMilliseconds { get; init; } = 1000;

    // 既定値を空にしているのは、Microsoft.Extensions.Configurationのバインダーが配列プロパティを
    // 「置換」ではなく「既存の既定値へ追記」するため。既定値が非空だとhataori.json側のproviderPriorityと
    // 結合されて重複扱いになりValidateOnStartが失敗する。空欄未設定時のfallbackはPostConfigure/呼び出し側で行う。
    public IReadOnlyList<string> ProviderPriority { get; set; } = [];
    public Dictionary<string, int> MaxConcurrentRuns { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Activationが監視するWorkspace root設定です。</summary>
public sealed class ActivationWorkspaceOptions
{
    public string WorkspaceId { get; init; } = Hataori.Core.Workspaces.WorkspaceId.Default;
    public string WorkingDirectory { get; init; } = string.Empty;
}

/// <summary>新旧Activation設定を実行時のWorkspace一覧へ正規化します。</summary>
public static class ActivationWorkspaceResolver
{
    public static IReadOnlyList<ActivationWorkspace> Resolve(ActivationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configured = options.Workspaces.Count > 0
            ? options.Workspaces
            : [new ActivationWorkspaceOptions { WorkspaceId = options.WorkspaceId, WorkingDirectory = options.WorkingDirectory }];
        return configured
            .Select(workspace => new ActivationWorkspace(
                Hataori.Core.Workspaces.WorkspaceId.Normalize(workspace.WorkspaceId),
                workspace.WorkingDirectory))
            .ToArray();
    }
}

/// <summary>正規化済みのActivation Workspaceです。</summary>
public sealed record ActivationWorkspace(string WorkspaceId, string WorkingDirectory);
