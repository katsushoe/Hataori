namespace Hataori.Cli;

/// <summary>環境変数を対象スコープから読み書きします。</summary>
public interface IEnvironmentVariableStore
{
    /// <summary>環境変数を取得します。</summary>
    string? Get(string name, EnvironmentVariableTarget target);

    /// <summary>環境変数を設定します。</summary>
    void Set(string name, string value, EnvironmentVariableTarget target);
}

/// <summary>OSの環境変数ストアを使用します。</summary>
public sealed class SystemEnvironmentVariableStore : IEnvironmentVariableStore
{
    /// <inheritdoc />
    public string? Get(string name, EnvironmentVariableTarget target) => Environment.GetEnvironmentVariable(name, target);

    /// <inheritdoc />
    public void Set(string name, string value, EnvironmentVariableTarget target) => Environment.SetEnvironmentVariable(name, value, target);
}

/// <summary>Itogurumaの既存認証トークンを値を表示せずHataoriへ連携します。</summary>
public sealed class ItogurumaSetupService(IEnvironmentVariableStore environment)
{
    public const string SourceVariable = "ITOGURUMA_AUTH_TOKEN";
    public const string TargetVariable = "HATAORI_ITOGURUMA__AUTHENTICATIONTOKEN";

    /// <summary>現在のユーザーに発行済みのトークンをHataoriへ連携します。</summary>
    public ItogurumaSetupResult Configure()
    {
        var token = environment.Get(SourceVariable, EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "No Itoguruma authentication token was found. Install or repair Itoguruma first; its installer creates the token automatically.");
        }

        environment.Set(TargetVariable, token, EnvironmentVariableTarget.User);
        environment.Set(TargetVariable, token, EnvironmentVariableTarget.Process);
        return new ItogurumaSetupResult(true, SourceVariable, TargetVariable);
    }
}

/// <summary>Itoguruma認証設定の結果です。秘密値は保持しません。</summary>
public sealed record ItogurumaSetupResult(bool Configured, string SourceVariable, string TargetVariable);
