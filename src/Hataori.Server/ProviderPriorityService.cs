using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hataori.Server;

/// <summary>Provider優先順位をHataori設定ファイルで管理します。</summary>
public sealed class ProviderPriorityService(string configurationPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>現在のProvider優先順位を取得します。</summary>
    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken)
    {
        var root = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return ReadPriority(root);
    }

    /// <summary>Provider優先順位を検証して保存します。</summary>
    public async Task<IReadOnlyList<string>> SetAsync(IReadOnlyList<string> providers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var normalized = providers.Select(provider => provider.Trim()).ToArray();
        if (normalized.Length == 0 || normalized.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty provider is required.", nameof(providers));
        }
        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            throw new ArgumentException("Provider priority must not contain duplicates.", nameof(providers));
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var root = await ReadAsync(cancellationToken).ConfigureAwait(false);
            var activation = root[ActivationOptions.SectionName] as JsonObject
                ?? throw new InvalidOperationException("Activation configuration is missing.");
            activation["providerPriority"] = new JsonArray(normalized.Select(provider => JsonValue.Create(provider)).ToArray());
            var temporaryPath = configurationPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, root.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, configurationPath, true);
            return normalized;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<JsonObject> ReadAsync(CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(configurationPath);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false) as JsonObject
            ?? throw new InvalidOperationException("Hataori configuration root must be a JSON object.");
    }

    private static IReadOnlyList<string> ReadPriority(JsonObject root)
    {
        var priority = root[ActivationOptions.SectionName]?["providerPriority"] as JsonArray;
        if (priority is null)
        {
            return new ActivationOptions().ProviderPriority;
        }
        return priority.Select(node => node?.GetValue<string>() ?? string.Empty).ToArray();
    }
}
