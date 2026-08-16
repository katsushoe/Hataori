using Hataori.Infrastructure.Agents.ClaudeCode;
using Hataori.Infrastructure.Agents.Codex;
using Hataori.Infrastructure.Itoguruma;
using Hataori.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Hataori.Cli;

/// <summary>
/// Hataori設定の参照と検証を提供します。
/// </summary>
public sealed class CliConfigurationManager
{
    private const string MaskedValue = "(redacted)";

    /// <summary>
    /// 設定管理コマンドを実行します。
    /// </summary>
    public Task<object> ExecuteAsync(string command, string configPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(configPath);
        if (command.Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<object>(new { path = fullPath, exists = File.Exists(fullPath) });
        }

        var configuration = LoadConfiguration(fullPath);
        if (command.Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            var values = configuration.AsEnumerable()
                .Where(pair => pair.Value is not null)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(pair => pair.Key, pair => IsSecret(pair.Key) ? MaskedValue : pair.Value, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<object>(new { path = fullPath, values });
        }

        if (command.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            var errors = ValidateConfiguration(configuration);
            return Task.FromResult<object>(new { path = fullPath, valid = errors.Count == 0, errors });
        }

        throw new ArgumentException($"Unknown config command '{command}'.");
    }

    /// <summary>
    /// JSONとHATAORI_環境変数をServerと同じ順序で読み込みます。
    /// </summary>
    public static IConfigurationRoot LoadConfiguration(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Hataori configuration file was not found.", fullPath);
        }

        return new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory)
            .AddJsonFile(Path.GetFileName(fullPath), optional: false, reloadOnChange: false)
            .AddEnvironmentVariables("HATAORI_")
            .Build();
    }

    /// <summary>
    /// Server起動時と同じValidator群で設定を検証します。
    /// </summary>
    public static IReadOnlyList<string> ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();
        Add(errors, new ServerOptionsValidator().Validate(null, Bind<ServerOptions>(configuration, ServerOptions.SectionName)));
        Add(errors, new ItogurumaClientOptionsValidator().Validate(null, Bind<ItogurumaClientOptions>(configuration, ItogurumaClientOptions.SectionName)));
        Add(errors, new CodexDriverOptionsValidator().Validate(null, Bind<CodexDriverOptions>(configuration, CodexDriverOptions.SectionName)));
        Add(errors, new ClaudeCodeDriverOptionsValidator().Validate(null, Bind<ClaudeCodeDriverOptions>(configuration, ClaudeCodeDriverOptions.SectionName)));
        Add(errors, new ActivationOptionsValidator().Validate(null, Bind<ActivationOptions>(configuration, ActivationOptions.SectionName)));
        Add(errors, new ReplyRetryOptionsValidator().Validate(null, Bind<ReplyRetryOptions>(configuration, ReplyRetryOptions.SectionName)));
        Add(errors, new FileLogOptionsValidator().Validate(null, Bind<FileLogOptions>(configuration, FileLogOptions.SectionName)));
        var hookSection = configuration.GetSection(HookOptions.SectionName);
        var hooks = hookSection.Exists() ? hookSection.Get<HookOptions>() : null;
        if (hooks?.Enabled == true && (string.IsNullOrWhiteSpace(hooks.CodexConfigPath) || string.IsNullOrWhiteSpace(hooks.ClaudeConfigPath)))
        {
            errors.Add("Enabled hooks require codexConfigPath and claudeConfigPath.");
        }
        return errors;
    }

    private static T Bind<T>(IConfiguration configuration, string sectionName) where T : class, new() =>
        configuration.GetRequiredSection(sectionName).Get<T>() ?? new T();

    private static void Add(List<string> errors, ValidateOptionsResult result)
    {
        if (result.Failed)
        {
            errors.AddRange(result.Failures);
        }
    }

    private static bool IsSecret(string key) =>
        key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith("key", StringComparison.OrdinalIgnoreCase);
}
