using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Hataori.Server;

namespace Hataori.Cli;

/// <summary>サービス専用の認証設定を永続化します。</summary>
public interface IWindowsServiceCredentialStore
{
    /// <summary>認証トークンをサービス専用設定へ保存します。</summary>
    Task<string> WriteAuthenticationTokenAsync(string token, CancellationToken cancellationToken);
}

/// <summary>ProgramDataへLocalSystem・Administrators限定で認証設定を保存します。</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceCredentialStore : IWindowsServiceCredentialStore
{
    /// <inheritdoc />
    public async Task<string> WriteAuthenticationTokenAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var path = ServiceConfigurationPath.GetDefaultPath();
        var directoryPath = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Service configuration directory could not be resolved.");
        try
        {
            Directory.CreateDirectory(directoryPath);
            SetDirectoryAccess(directoryPath);
            var json = JsonSerializer.Serialize(new { itoguruma = new { authenticationToken = token } });
            await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            SetFileAccess(path);
            return path;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException("Service setup requires an administrator terminal. Run the command again as administrator.", exception);
        }
    }

    private static void SetDirectoryAccess(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void SetFileAccess(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}

/// <summary>発行済みItogurumaトークンをサービス専用設定へ値を表示せず連携します。</summary>
public sealed class WindowsServiceSetupService(IEnvironmentVariableStore environment, IWindowsServiceCredentialStore store)
{
    /// <summary>サービス専用認証設定を作成します。</summary>
    public async Task<WindowsServiceSetupResult> ConfigureAsync(CancellationToken cancellationToken)
    {
        var token = environment.Get(ItogurumaSetupService.SourceVariable, EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("No Itoguruma authentication token was found. Install or repair Itoguruma first; its installer creates the token automatically.");
        }

        var path = await store.WriteAuthenticationTokenAsync(token, cancellationToken).ConfigureAwait(false);
        return new WindowsServiceSetupResult(true, path, true);
    }
}

/// <summary>Windows Service専用設定の作成結果です。秘密値は保持しません。</summary>
public sealed record WindowsServiceSetupResult(bool Configured, string ConfigurationPath, bool RestartRequired);
