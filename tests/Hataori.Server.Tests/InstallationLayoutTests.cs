using FluentAssertions;

using Hataori.Application;

namespace Hataori.Server.Tests;

public sealed class InstallationLayoutTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"hataori-layout-{Guid.NewGuid():N}");

    [Fact]
    public void Resolve_StandardDirectoryTree_ReturnsInstallRoot()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "bin", "server"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "config"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "logs"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "data"));

        var result = InstallationLayout.Resolve(Path.Combine(_rootPath, "bin", "server"));

        result.RootPath.Should().Be(Path.GetFullPath(_rootPath));
        result.ConfigurationPath.Should().Be(Path.Combine(_rootPath, "config", "hataori.json"));
    }

    [Fact]
    public void Resolve_BeforeRuntimeDirectoriesExist_ReturnsInstallRoot()
    {
        Directory.CreateDirectory(Path.Combine(_rootPath, "bin", "cli"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "config"));

        var result = InstallationLayout.Resolve(Path.Combine(_rootPath, "bin", "cli"));

        result.RootPath.Should().Be(Path.GetFullPath(_rootPath));
        result.ConfigurationPath.Should().Be(Path.Combine(_rootPath, "config", "hataori.json"));
    }

    [Fact]
    public void Resolve_DevelopmentOutputWithoutStandardTree_UsesApplicationDirectory()
    {
        var applicationPath = Path.Combine(_rootPath, "bin", "Debug", "net9.0");
        Directory.CreateDirectory(applicationPath);

        var result = InstallationLayout.Resolve(applicationPath);

        result.RootPath.Should().Be(Path.GetFullPath(applicationPath));
    }

    [Fact]
    public async Task EnsureAsync_MissingConfiguration_CreatesDefaultWithoutSecret()
    {
        var path = Path.Combine(_rootPath, "config", "hataori.json");

        var created = await DefaultConfigurationWriter.EnsureAsync(path, CancellationToken.None);
        var content = await File.ReadAllTextAsync(path);

        created.Should().BeTrue();
        content.Should().Contain("\"language\": \"ja-JP\"");
        content.Should().Contain("\"databasePath\": \"data/hataori.db\"");
        content.Should().NotContain("authenticationToken");
    }

    [Fact]
    public async Task EnsureAsync_EnglishLanguage_CreatesEnglishConfiguration()
    {
        var path = Path.Combine(_rootPath, "config", "hataori.json");

        var created = await DefaultConfigurationWriter.EnsureAsync(path, "en-US", CancellationToken.None);

        created.Should().BeTrue();
        (await File.ReadAllTextAsync(path)).Should().Contain("\"language\": \"en-US\"");
    }

    [Fact]
    public async Task EnsureAsync_UnsupportedLanguage_ThrowsArgumentException()
    {
        var path = Path.Combine(_rootPath, "config", "hataori.json");

        var action = () => DefaultConfigurationWriter.EnsureAsync(path, "fr-FR", CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EnsureAsync_ExistingConfiguration_PreservesContent()
    {
        var path = Path.Combine(_rootPath, "config", "hataori.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "custom");

        var created = await DefaultConfigurationWriter.EnsureAsync(path, CancellationToken.None);

        created.Should().BeFalse();
        (await File.ReadAllTextAsync(path)).Should().Be("custom");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
