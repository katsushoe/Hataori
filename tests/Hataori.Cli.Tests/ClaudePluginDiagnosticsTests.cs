using FluentAssertions;

namespace Hataori.Cli.Tests;

public sealed class ClaudePluginDiagnosticsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hataori-claude-plugin-tests-{Guid.NewGuid():N}");

    public ClaudePluginDiagnosticsTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task CheckAsync_WhenPluginEnabled_Completes()
    {
        var path = await WriteSettingsAsync("""
            { "enabledPlugins": { "kotodama-spec-guard@katsushoe-private": true } }
            """);

        var action = () => ClaudePluginDiagnostics.CheckAsync(path, "kotodama-spec-guard@katsushoe-private", CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"enabledPlugins\": {} }")]
    [InlineData("{ \"enabledPlugins\": { \"kotodama-spec-guard@katsushoe-private\": false } }")]
    public async Task CheckAsync_WhenPluginNotEnabled_Throws(string json)
    {
        var path = await WriteSettingsAsync(json);

        var action = () => ClaudePluginDiagnostics.CheckAsync(path, "kotodama-spec-guard@katsushoe-private", CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CheckAsync_WhenSettingsMissing_Throws()
    {
        var path = Path.Combine(_directory, "missing.json");

        var action = () => ClaudePluginDiagnostics.CheckAsync(path, "kotodama-spec-guard@katsushoe-private", CancellationToken.None);

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task<string> WriteSettingsAsync(string json)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
