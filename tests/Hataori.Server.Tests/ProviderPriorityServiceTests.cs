using System.Text.Json;
using FluentAssertions;

namespace Hataori.Server.Tests;

public sealed class ProviderPriorityServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"hataori-provider-config-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task SetAsync_ValidProviders_PersistsAndReturnsPriority()
    {
        await File.WriteAllTextAsync(_path, """{"activation":{"providerPriority":["codex"]},"other":{"preserved":true}}""");
        var service = new ProviderPriorityService(_path);

        var result = await service.SetAsync(["claude-code", "codex"], CancellationToken.None);

        result.Should().Equal("claude-code", "codex");
        (await service.GetAsync(CancellationToken.None)).Should().Equal(result);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_path));
        document.RootElement.GetProperty("other").GetProperty("preserved").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_DuplicateProvider_ThrowsArgumentException()
    {
        await File.WriteAllTextAsync(_path, """{"activation":{"providerPriority":["codex"]}}""");
        var service = new ProviderPriorityService(_path);

        var action = () => service.SetAsync(["codex", "CODEX"], CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_LegacyConfigurationWithoutPriority_ReturnsDefaultPriority()
    {
        await File.WriteAllTextAsync(_path, """{"activation":{"enabled":true}}""");
        var service = new ProviderPriorityService(_path);

        var result = await service.GetAsync(CancellationToken.None);

        result.Should().Equal("codex", "claude-code");
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
        if (File.Exists(_path + ".tmp"))
        {
            File.Delete(_path + ".tmp");
        }
        GC.SuppressFinalize(this);
    }
}
