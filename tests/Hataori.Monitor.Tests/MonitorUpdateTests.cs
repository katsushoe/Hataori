using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class MonitorUpdateTests
{
    private static readonly byte[] Package = "test MSI content"u8.ToArray();
    private static readonly string Digest = Convert.ToHexString(SHA256.HashData(Package));

    [Fact]
    public void ParseRelease_selects_only_new_stable_four_part_versions()
    {
        using var release = Release();
        Assert.Equal(new Version(3, 1, 24, 0), MonitorUpdateClient.ParseRelease(release.RootElement, new Version(3, 1, 23, 0))!.Version);
        Assert.Null(MonitorUpdateClient.ParseRelease(release.RootElement, new Version(3, 1, 24, 0)));
        Assert.Null(MonitorUpdateClient.ParseRelease(release.RootElement, new Version(3, 1, 25, 0)));
        using var preview = Release(prerelease: true);
        Assert.Null(MonitorUpdateClient.ParseRelease(preview.RootElement, new Version(3, 1, 23, 0)));
        using var draft = Release(draft: true);
        Assert.Null(MonitorUpdateClient.ParseRelease(draft.RootElement, new Version(3, 1, 23, 0)));
    }

    [Theory]
    [InlineData("v3.1.24", "sha256:invalid", "https://example.com/update.msi", 16)]
    [InlineData("v3.1.24.0", "sha256:invalid", "https://github.com/katsushoe/Hataori/releases/download/v3.1.24.0/Hataori-3.1.24.0-x64.msi", 16)]
    [InlineData("v3.1.24.0", null, "http://github.com/katsushoe/Hataori/releases/download/v3.1.24.0/Hataori-3.1.24.0-x64.msi", 16)]
    [InlineData("v3.1.24.0", null, "https://example.com/update.msi", 16)]
    [InlineData("v3.1.24.0", null, null, 0)]
    [InlineData("v3.1.24.0", null, null, 536870913)]
    public void ParseRelease_rejects_invalid_versions_digests_urls_and_sizes(string tag, string? digest, string? url, long size)
    {
        using var release = Release(tag, digest, url, size);
        Assert.Throws<InvalidDataException>(() => MonitorUpdateClient.ParseRelease(release.RootElement, new Version(3, 1, 23, 0)));
    }

    [Fact]
    public async Task CheckAsync_uses_configured_endpoint_and_reports_http_errors()
    {
        using var release = Release();
        using var handler = new StubHandler(request =>
        {
            Assert.Equal("https://api.github.com/repos/katsushoe/Hataori/releases/latest", request.RequestUri!.AbsoluteUri);
            Assert.Contains("Hataori-Monitor", request.Headers.UserAgent.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(release.RootElement.GetRawText()) };
        });
        using var http = new HttpClient(handler);
        var client = new MonitorUpdateClient(http, new Uri("https://api.github.com/repos/katsushoe/Hataori/releases/latest"));
        Assert.NotNull(await client.CheckAsync(new Version(3, 1, 23, 0), CancellationToken.None));
        using var failedHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var failedHttp = new HttpClient(failedHandler);
        await Assert.ThrowsAsync<HttpRequestException>(() => new MonitorUpdateClient(failedHttp, new Uri("https://api.github.com/"))
            .CheckAsync(new Version(3, 1, 23, 0), CancellationToken.None));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Download_verifies_bytes_and_deletes_corrupt_or_truncated_packages(bool corrupt, bool truncated)
    {
        var directory = Path.Combine(Path.GetTempPath(), "hataori-update-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(truncated ? Package[..^1] : Package),
            });
            using var http = new HttpClient(handler);
            var client = new MonitorUpdateClient(http, new Uri("https://api.github.com/"));
            var release = new MonitorRelease(new Version(3, 1, 24, 0), new Uri("https://github.com/katsushoe/Hataori/releases/download/v3.1.24.0/Hataori-3.1.24.0-x64.msi"), corrupt ? new string('0', 64) : Digest, Package.Length);
            if (corrupt || truncated)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, directory, CancellationToken.None));
                Assert.Empty(Directory.EnumerateFiles(directory));
            }
            else Assert.Equal(Package, await File.ReadAllBytesAsync(await client.DownloadAsync(release, directory, CancellationToken.None)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Download_accepts_GitHub_asset_redirect_and_rejects_untrusted_redirect()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hataori-update-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var untrusted = false;
            using var handler = new StubHandler(request =>
            {
                if (request.RequestUri!.Host == "release-assets.githubusercontent.com")
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package) };
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.Location = new Uri(untrusted ? "https://example.com/payload.msi" : "https://release-assets.githubusercontent.com/payload.msi");
                return response;
            });
            using var http = new HttpClient(handler);
            var client = new MonitorUpdateClient(http, new Uri("https://api.github.com/"));
            var release = new MonitorRelease(new Version(3, 1, 24, 0), new Uri("https://github.com/katsushoe/Hataori/releases/download/v3.1.24.0/Hataori-3.1.24.0-x64.msi"), Digest, Package.Length);
            var file = await client.DownloadAsync(release, directory, CancellationToken.None);
            Assert.True(File.Exists(file));
            File.Delete(file);
            untrusted = true;
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, directory, CancellationToken.None));
            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Installer_preserves_custom_install_root_and_passes_paths_as_distinct_arguments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Hataori test space");
        var job = new MonitorUpdateJob(1, Path.Combine(directory, "update.msi"), Digest, Path.Combine(directory, "monitor.exe"), directory, "custom-pipe", "en-US");
        var start = MonitorUpdateInstaller.CreateInstallerStart(job);
        Assert.Equal("runas", start.Verb);
        Assert.Contains(job.PackagePath, start.ArgumentList);
        Assert.Contains("INSTALL_ROOT=" + directory, start.ArgumentList);
        Assert.Contains("/norestart", start.ArgumentList);
        Assert.DoesNotContain("/quiet", start.ArgumentList);
    }

    private static JsonDocument Release(string tag = "v3.1.24.0", string? digest = null, string? url = null, long size = 16, bool prerelease = false, bool draft = false) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            tag_name = tag,
            prerelease,
            draft,
            assets = new[] { new { name = "Hataori-3.1.24.0-x64.msi", browser_download_url = url ?? "https://github.com/katsushoe/Hataori/releases/download/v3.1.24.0/Hataori-3.1.24.0-x64.msi", digest = digest ?? "sha256:" + Digest, size } },
        }));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
