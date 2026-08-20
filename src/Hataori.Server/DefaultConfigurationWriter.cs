using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hataori.Server;

/// <summary>既定設定を標準構成へ初回生成します。</summary>
public static class DefaultConfigurationWriter
{
    private const string ResourceName = "Hataori.Server.hataori.json";
    private static readonly string[] SupportedLanguages = ["ja-JP", "en-US"];

    /// <summary>設定が存在しない場合だけ既定設定を作成します。</summary>
    public static async Task<bool> EnsureAsync(string path, CancellationToken cancellationToken)
        => await EnsureAsync(path, null, cancellationToken).ConfigureAwait(false);

    /// <summary>設定が存在しない場合だけ、指定言語を含む既定設定を作成します。</summary>
    public static async Task<bool> EnsureAsync(string path, string? language, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (language is not null && !SupportedLanguages.Contains(language, StringComparer.Ordinal))
        {
            throw new ArgumentException("Language must be ja-JP or en-US.", nameof(language));
        }

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Configuration directory could not be resolved."));
        await using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Default Hataori configuration resource was not found.");
        using var reader = new StreamReader(resource, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (language is not null)
        {
            var root = JsonNode.Parse(content)?.AsObject()
                ?? throw new InvalidOperationException("Default Hataori configuration is invalid.");
            var application = root["application"]?.AsObject()
                ?? throw new InvalidOperationException("Default Hataori application configuration is missing.");
            application["language"] = language;
            content = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
        }

        try
        {
            await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await using var writer = new StreamWriter(output, new UTF8Encoding(false));
            await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException) when (File.Exists(fullPath))
        {
            return false;
        }
    }
}
