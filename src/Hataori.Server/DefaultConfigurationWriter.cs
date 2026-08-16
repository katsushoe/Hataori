using System.Reflection;
using System.Text;

namespace Hataori.Server;

/// <summary>既定設定を標準構成へ初回生成します。</summary>
public static class DefaultConfigurationWriter
{
    private const string ResourceName = "Hataori.Server.hataori.json";

    /// <summary>設定が存在しない場合だけ既定設定を作成します。</summary>
    public static async Task<bool> EnsureAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
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
