using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hataori.Monitor;

internal sealed record MonitorRelease(Version Version, Uri DownloadUri, string Sha256, long Size);

/// <summary>公式GitHub ReleaseのMSIだけを取得し、APIのSHA-256と照合します。</summary>
internal sealed class MonitorUpdateClient(HttpClient client, Uri endpoint)
{
    internal const long MaximumPackageSize = 512L * 1024 * 1024;

    public async Task<MonitorRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.UserAgent.ParseAdd("Hataori-Monitor/" + currentVersion);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response.Content, 1024 * 1024, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(bytes);
        return ParseRelease(document.RootElement, currentVersion);
    }

    internal static MonitorRelease? ParseRelease(JsonElement release, Version currentVersion)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version.Revision < 0)
            throw new InvalidDataException("Release version must have four numeric components.");
        if (version <= currentVersion) return null;
        var expectedName = $"Hataori-{version}-x64.msi";
        var assets = release.GetProperty("assets").EnumerateArray()
            .Where(asset => asset.GetProperty("name").GetString() == expectedName).ToArray();
        if (assets.Length != 1) throw new InvalidDataException("The release must contain exactly one matching x64 MSI.");
        var asset = assets[0];
        var expectedUrl = $"https://github.com/katsushoe/Hataori/releases/download/{tag}/{expectedName}";
        if (asset.GetProperty("browser_download_url").GetString() != expectedUrl)
            throw new InvalidDataException("The update asset is outside the official release.");
        var digest = asset.GetProperty("digest").GetString() ?? string.Empty;
        if (!digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("The release asset has no valid SHA-256 digest.");
        var size = asset.GetProperty("size").GetInt64();
        if (size is <= 0 or > MaximumPackageSize) throw new InvalidDataException("Invalid update asset size.");
        return new MonitorRelease(version, new Uri(expectedUrl), digest[7..], size);
    }

    public async Task<string> DownloadAsync(MonitorRelease release, string directory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Hataori-{release.Version}-x64.msi");
        var created = false;
        try
        {
            using var response = await GetAssetResponseAsync(release.DownloadUri, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true))
            {
                created = true;
                var buffer = new byte[81920];
                long count = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    count += read;
                    if (count > release.Size || count > MaximumPackageSize) throw new InvalidDataException("Update exceeds its declared size.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
                if (count != release.Size) throw new InvalidDataException("Incomplete update download.");
                output.Position = 0;
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(output, cancellationToken).ConfigureAwait(false));
                if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update SHA-256 mismatch.");
            }
            return path;
        }
        catch
        {
            if (created && File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private async Task<HttpResponseMessage> GetAssetResponseAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var redirect = 0; redirect < 6; redirect++)
        {
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                !(uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Untrusted update download redirect.");
            var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new InvalidDataException("Update redirect has no location.");
            uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
        }
        throw new InvalidDataException("Too many update download redirects.");
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximum, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > maximum) throw new InvalidDataException("Release response is too large.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }
}
